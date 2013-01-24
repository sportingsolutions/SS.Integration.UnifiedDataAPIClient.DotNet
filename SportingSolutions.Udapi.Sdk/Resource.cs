//Copyright 2012 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource, IDisposable, IStreamStatistics
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(Resource).ToString());

        private bool _isStreaming;
        private readonly ManualResetEvent _pauseStream;
        private readonly AutoResetEvent _echoResetEvent;
        private readonly AutoResetEvent _echoTimerEvent;

        private IModel _channel;
        private IConnection _connection;
        private QueueingCustomConsumer _consumer;
        private string _virtualHost;
        private string _queueName;

        private int _echoSenderInterval;
        private int _echoMaxDelay;
        
        private string _lastRecievedEchoGuid;

        private int _disconnections;
        private int _maxRetries;
        private ConnectionFactory _connectionFactory;
        
        private bool _isReconnecting;

        private Task _echoTask;
        private CancellationTokenSource _echoTokenSource;

        private bool _isProcessingStreamEvent;


        internal Resource(NameValueCollection headers, RestItem restItem)
            : base(headers, restItem)
        {
            _logger.DebugFormat("Instantiated fixtureName=\"{0}\"", restItem.Name);
            _pauseStream = new ManualResetEvent(true);
            _echoResetEvent = new AutoResetEvent(false);
            _echoTimerEvent = new AutoResetEvent(true);
        }

        public string Id
        {
            get { return State.Content.Id; }
        }

        public string Name
        {
            get { return State.Name; }
        }

        public DateTime LastMessageReceived { get; private set; }
        public DateTime LastStreamDisconnect { get; private set; }
        public bool IsStreamActive { get { return _isStreaming; } }
        public double EchoRoundTripInMilliseconds { get; private set; }

        public Summary Content
        {
            get { return State.Content; }
        }

        public string GetSnapshot()
        {
            _logger.InfoFormat("Get Snapshot for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            return FindRelationAndFollowAsString("http://api.sportingsolutions.com/rels/snapshot");
        }

        public void StartStreaming()
        {
            StartStreaming(10000,3000);
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            _logger.InfoFormat("Starting stream for fixtureName=\"{0}\" fixtureId={1} with Echo Interval of {2}",Name, Id, echoInterval);

            _echoSenderInterval = echoInterval;
            _echoMaxDelay = echoMaxDelay;

            if (State != null)
            {
                Task.Factory.StartNew(StreamData,TaskCreationOptions.LongRunning);
            }
        }

        private void StreamData()
        {
            _connectionFactory = new ConnectionFactory();

            _maxRetries = 1;
            _disconnections = 0;

            _isStreaming = true;

            Reconnect();
            
            _logger.InfoFormat("Initialised connection to Streaming Queue for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            
            _consumer.QueueCancelled += Dispose;

            while (_isStreaming)
            {
                try
                {
                    _pauseStream.WaitOne();
                    var output = _consumer.Queue.Dequeue();
                    if (output != null)
                    {
                        var deliveryArgs = (BasicDeliverEventArgs)output;
                        var message = deliveryArgs.Body;
                        if (StreamEvent != null)
                        {
                            LastMessageReceived = DateTime.UtcNow;

                            var messageString = Encoding.UTF8.GetString(message);
                            var jobject = JObject.Parse(messageString);
                            if(jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
                            {
                                var split = jobject["Content"].Value<String>().Split(';');
                                _lastRecievedEchoGuid = split[0];
                                var timeSent = DateTime.ParseExact(split[1], "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                                var roundTripTime = DateTime.Now - timeSent;

                                var roundMillis = roundTripTime.TotalMilliseconds;

                                EchoRoundTripInMilliseconds = roundMillis;
                                _echoResetEvent.Set();
                            }
                            else
                            {
                                _isProcessingStreamEvent = true;      
                                StreamEvent(this, new StreamEventArgs(messageString));
                                _isProcessingStreamEvent = false;
                            }
                        }
                    }
                    _disconnections = 0;
                }
                catch(Exception ex)
                {
                    _logger.Error(string.Format("Lost connection to stream for fixtureName=\"{0}\" fixtureId={1}", Name, Id), ex);
                    LastStreamDisconnect = DateTime.UtcNow;
                    //connection lost
                    if(!_isReconnecting)
                    {
                        StopEcho();
                        Reconnect();   
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        private void Reconnect()
        {
            _logger.WarnFormat("Attempting to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}",Name,Id,_disconnections+1);
            var success = false;
            while (!success && _isStreaming)
            {
                try
                {
                    var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp");
                    var amqpLink = restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

                    var amqpUri = new Uri(amqpLink.Href);

                    _connectionFactory.RequestedHeartbeat = 5;

                    var host = amqpUri.Host;
                    if (!String.IsNullOrEmpty(host))
                    {
                        _connectionFactory.HostName = host;
                    }
                    var port = amqpUri.Port;
                    if (port != -1)
                    {
                        _connectionFactory.Port = port;
                    }
                    var userInfo = amqpUri.UserInfo;
                    userInfo = HttpUtility.UrlDecode(userInfo);
                    if (!String.IsNullOrEmpty(userInfo))
                    {
                        var userPass = userInfo.Split(':');
                        if (userPass.Length > 2)
                        {
                            throw new ArgumentException(string.Format("Bad user info in AMQP URI: {0}", userInfo));
                        }
                        _connectionFactory.UserName = userPass[0];
                        if (userPass.Length == 2)
                        {
                            _connectionFactory.Password = userPass[1];
                        }
                    }
                    _queueName = "";
                    var path = amqpUri.AbsolutePath;
                    if (!String.IsNullOrEmpty(path))
                    {
                        _queueName = path.Substring(path.IndexOf('/', 1) + 1);
                        _virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);

                        _connectionFactory.VirtualHost = "/" + _virtualHost;
                    }

                    if (_channel != null)
                    {
                        _channel.Close();
                        _channel = null;
                    }

                    try
                    {
                        if (_connection != null)
                        {
                            if (_connection.IsOpen)
                            {
                                _connection.Close();
                            }
                            _connection = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                    }

                    _connection = _connectionFactory.CreateConnection();
                    _logger.InfoFormat("Successfully connected to Streaming Server for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
                    
                    StartEcho();
 
                    if (StreamConnected != null)
                    {
                        StreamConnected(this, new EventArgs());
                    }

                    _channel = _connection.CreateModel();
                    _consumer = new QueueingCustomConsumer(_channel);
                    _channel.BasicConsume(_queueName, true, _consumer);
                    _channel.BasicQos(0, 10, false);
                    success = true;
                }
                catch(Exception)
                {
                    if (_disconnections > _maxRetries)
                    {
                        _logger.ErrorFormat("Failed to reconnect Stream for fixtureName=\"{0}\" fixtureId={1} ",Name, Id);
                        StopStreaming();
                    }
                    else
                    {
                        // give time for load balancer to notice the node is down
                        Thread.Sleep(500);
                        _disconnections++;
                        _logger.WarnFormat("Failed to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}", Name, Id, _disconnections);      
                    }
                }
            }
        }

        private void StartEcho()
        {
            if (_echoTask == null)
            {
                _echoTokenSource = new CancellationTokenSource();
                var cancelToken = _echoTokenSource.Token;
                _echoResetEvent.Reset();
                _echoTask = Task.Factory.StartNew(() => SendEcho(cancelToken), cancelToken);
            }
        }
        
        private void StopEcho()
        {
            if (_echoTask != null)
            {
                _echoTokenSource.Cancel();
            }
        }

        private void SendEcho(CancellationToken cancelToken)
        {
            var echoGuid = Guid.NewGuid().ToString();

            while (_isStreaming)
            {
                var indexofSignal = WaitHandle.WaitAny(new[] { _echoTimerEvent, cancelToken.WaitHandle }, _echoSenderInterval);
                if (indexofSignal == 1)
                {
                    return;
                }

                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }

                if (_isProcessingStreamEvent) continue;

                try
                {
                    if (State != null)
                    {
                        var theLink =
                            State.Links.First(
                                restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/stream/echo");
                        var theUrl = theLink.Href;

                        var streamEcho = new StreamEcho
                        {
                            Host = _virtualHost,
                            Queue = _queueName,
                            Message = echoGuid + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                        };

                        var stringStreamEcho = streamEcho.ToJson();

                        RestHelper.GetResponse(new Uri(theUrl), stringStreamEcho, "POST", "application/json",
                                               Headers, 3000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Unable to post echo for fixtureName=\"{0}\" fixtureId={1}", Name, Id), ex);
                }

                var waitHandleResult = WaitHandle.WaitAny(new[] { _echoResetEvent, cancelToken.WaitHandle }, _echoMaxDelay);
                var echoArrived = false;
                if (waitHandleResult == 0)
                {
                    echoArrived = true;
                }
                else if (waitHandleResult == 1)
                {
                    return;
                }
                _echoResetEvent.Reset();

                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }

                //signal was recieved
                if (echoArrived)
                {
                    if (echoGuid.Equals(_lastRecievedEchoGuid))
                    {
                        _logger.DebugFormat("Echo recieved for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
                    }
                    else
                    {
                        _logger.Error("Recieved Echo Messages from differerent client");
                    }
                }
                else
                {
                    if (!_isProcessingStreamEvent)
                    {
                        _logger.InfoFormat("No echo recieved for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
                        LastStreamDisconnect = DateTime.UtcNow;
                        //reached timeout, no echo has arrived
                        _isReconnecting = true;
                        Reconnect();

                        _echoTimerEvent.Set();
                        _isReconnecting = false;
                        if (cancelToken.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                }
            }
            _logger.DebugFormat("Echo successfully cancelled for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
        }

        public void PauseStreaming()
        {
            _logger.InfoFormat("Streaming paused for fixtureName=\"{0}\" fixtureId={1}",Name, Id);
            _pauseStream.Reset();
            StopEcho();
        }

        public void UnPauseStreaming()
        {
            _logger.InfoFormat("Streaming unpaused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            _pauseStream.Set();
            StartEcho();
        }

        public void StopStreaming()
        {
            _isStreaming = false;
            StopEcho();
            if (_consumer != null)
            {
                try
                {
                    _channel.BasicCancel(_consumer.ConsumerTag);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Problem when stopping stream for fixtureId={0} fixtureName=\"{1}\"",Id, Name),ex);
                    Dispose();
                }
            }
            else
            {
                Dispose();
            }
        }

        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
        public event EventHandler StreamSynchronizationError;

        public void Dispose()
        {
            _logger.InfoFormat("Streaming stopped for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            if(_channel != null)
            {
                try
                {
                    _channel.Close();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
                _channel = null;
            }
            if(_connection != null)
            {
                try
                {
                    if (_connection.IsOpen)
                    {
                        _connection.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
                _connection = null;
            }
            
            _echoTask = null;
            
            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());
            }
        }
    }
}
