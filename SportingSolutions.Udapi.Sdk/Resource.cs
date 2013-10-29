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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource, IDisposable, IStreamStatistics
    {
        private bool _isStreaming;
        private readonly ManualResetEvent _pauseStream;

        private IModel _channel;
        private IConnection _connection;
        private QueueingCustomConsumer _consumer;
        private string _virtualHost;
        private string _queueName;

        private int _echoSenderInterval;

        private int _reconnectionsSinceLastMessage;
        private int _maxRetries;
        private ConnectionFactory _connectionFactory;

        private bool _isReconnecting;

        private string _lastSentGuid;

        private CancellationTokenSource _cancellationTokenSource;

        internal Resource(RestItem restItem, IConnectClient connectClient)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Resource).ToString());
            Logger.DebugFormat("Instantiated fixtureName=\"{0}\"", restItem.Name);
            _pauseStream = new ManualResetEvent(true);
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
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get Snapshot for fixtureName=\"{0}\" fixtureId={1} \r\n", Name, Id);

            var result = FindRelationAndFollowAsString("http://api.sportingsolutions.com/rels/snapshot", "GetSnapshot Http Error", loggingStringBuilder);
            Logger.Info(loggingStringBuilder);
            return result;
        }

        public void StartStreaming()
        {
            StartStreaming(10000, 3000);
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            Logger.InfoFormat("Starting stream for fixtureName=\"{0}\" fixtureId={1} with Echo Interval of {2}", Name, Id, echoInterval);

            _echoSenderInterval = echoInterval;

            if (State != null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _cancellationTokenSource.Token;
                var streamTask = Task.Factory.StartNew(() => StreamData(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                streamTask.ContinueWith(t => Logger.InfoFormat("Stream cancelled for fixtureName=\"{0}\" fixtureId={1}", Name, Id), TaskContinuationOptions.OnlyOnCanceled);
            }
        }

        private void StreamData(CancellationToken cancellationToken)
        {
            _connectionFactory = new ConnectionFactory();

            var missedEchos = 0;

            _maxRetries = 5;
            _isStreaming = true;

            Reconnect();

            Logger.InfoFormat("Initialised connection to Streaming Queue for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            LastMessageReceived = DateTime.UtcNow;

            _consumer.QueueCancelled += Dispose;
            bool isExpectingEcho = false;

            while (_isStreaming && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _pauseStream.WaitOne();
                    object output = null;

                    if (_consumer.Queue.Dequeue(_echoSenderInterval, out output))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        var deliveryArgs = (BasicDeliverEventArgs)output;
                        var message = deliveryArgs.Body;
                        if (StreamEvent != null)
                        {
                            LastMessageReceived = DateTime.UtcNow;

                            var messageString = Encoding.UTF8.GetString(message);
                            var jobject = JObject.Parse(messageString);
                            if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
                            {
                                ProcessEcho(jobject,isExpectingEcho);
                                isExpectingEcho = false;
                            }
                            else
                            {
                                isExpectingEcho = false;
                                StreamEvent(this, new StreamEventArgs(messageString));
                            }
                        }
                    }
                    else
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        // message didn't arrive in specified time period
                        if (isExpectingEcho)
                        {
                            missedEchos++;
                            Logger.InfoFormat("No echo recieved for fixtureId={0} fixtureName=\"{1}\" missedEchos={2}", Id, Name, missedEchos);

                            if (missedEchos > 3)
                            {
                                Logger.WarnFormat("Missed 3 echos reconnecting stream for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
                                LastStreamDisconnect = DateTime.UtcNow;
                                //reached timeout, no echo has arrived
                                _isReconnecting = true;
                                Reconnect();
                                missedEchos = 0;
                                _isReconnecting = false;
                                isExpectingEcho = false;   
                            }
                        }
                        else
                        {
                            isExpectingEcho = SendEcho();
                        }
                    }
                    _reconnectionsSinceLastMessage = 0;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested || _isStreaming)
                    {
                        Logger.Error(string.Format("Lost connection to stream for fixtureName=\"{0}\" fixtureId={1}", Name, Id), ex);
                        LastStreamDisconnect = DateTime.UtcNow;
                        //connection lost
                        if (!_isReconnecting)
                        {
                            Reconnect();
                            missedEchos = 0;
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }   
                    }
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void ProcessEcho(JObject jobject,bool isExpectingEcho)
        {
            if (!isExpectingEcho)
            {
                Logger.DebugFormat("Echo arrived but will be ignored since it's not expected. This may be due to earlier update clashing with echo");
            }

            var split = jobject["Content"].Value<String>().Split(';');
            var recievedEchoGuid = split[0];
            var timeSent = DateTime.ParseExact(split[1], "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var roundTripTime = DateTime.Now - timeSent;

            var roundMillis = roundTripTime.TotalMilliseconds;

            EchoRoundTripInMilliseconds = roundMillis;

            //signal was recieved

            if (_lastSentGuid == recievedEchoGuid)
            {
                Logger.DebugFormat("Echo recieved for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
            }
            else
            {
                Logger.ErrorFormat("Recieved Echo Messages from differerent client for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
            }
        }

        private void Reconnect()
        {
            Logger.WarnFormat("Attempting to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}", Name, Id, _reconnectionsSinceLastMessage + 1);
            var success = false;
            while (!success && _isStreaming)
            {
                try
                {
                    if (_reconnectionsSinceLastMessage > _maxRetries)
                    {
                        Logger.ErrorFormat("Failed to reconnect Stream for fixtureName=\"{0}\" fixtureId={1} ", Name,
                                            Id);
                        StopStreaming();
                        return;
                    }

                    var loggingStringBuilder = new StringBuilder();
                    var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp", "GetAmqpStream Http Error", loggingStringBuilder);
                    Logger.Info(loggingStringBuilder);
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
                        Logger.Error(ex);
                    }

                    _connection = _connectionFactory.CreateConnection();
                    Logger.InfoFormat("Successfully connected to Streaming Server for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

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
                catch (Exception ex)
                {
                    // give time for load balancer to notice the node is down
                    Thread.Sleep(500);
                    
                    Logger.Warn(string.Format("Failed to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}", Name, Id,
                        _reconnectionsSinceLastMessage + 1), ex);
                }
                finally
                {
                    _reconnectionsSinceLastMessage++;
                }
            }
        }

        private bool SendEcho()
        {
            _lastSentGuid = Guid.NewGuid().ToString();

            try
            {
                if (State != null)
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    var theLink =
                        State.Links.First(
                            restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/stream/echo");
                    var theUrl = theLink.Href;

                    var streamEcho = new StreamEcho
                    {
                        Host = _virtualHost,
                        Queue = _queueName,
                        Message = _lastSentGuid + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    };

                    var response = ConnectClient.Request(new Uri(theUrl), Method.POST, streamEcho,"application/json", 7000);

                    if (response == null)
                    {
                        Logger.WarnFormat("Post Echo for fixtureName=\"{0}\" fixtureId={1} had null response and took {2}ms", Name, Id, stopwatch.ElapsedMilliseconds);
                        return false;
                    }
                    if (response.ErrorException != null)
                    {
                        RestErrorHelper.LogRestError(Logger, response, string.Format("Echo Http Error fixtureName=\"{0}\" fixtureId={1} took {2}ms", Name, Id, stopwatch.ElapsedMilliseconds));
                        return false;
                    }
                  
                    Logger.DebugFormat("Post Echo for fixtureName=\"{0}\" fixtureId={1} took duration={2}ms", Name, Id, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Unable to post echo for fixtureName=\"{0}\" fixtureId={1}", Name, Id), ex);
                return false;
            }

            return true;
        }

        public void PauseStreaming()
        {
            Logger.InfoFormat("Streaming paused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            _pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            Logger.InfoFormat("Streaming unpaused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            _pauseStream.Set();
        }

        public void StopStreaming()
        {
            _isStreaming = false;
            _cancellationTokenSource.Cancel();
            if (_consumer != null)
            {
                try
                {
                    _channel.BasicCancel(_consumer.ConsumerTag);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Problem when stopping stream for fixtureId={0} fixtureName=\"{1}\"", Id, Name), ex);
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
            Logger.InfoFormat("Streaming stopped for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            if (_channel != null)
            {
                try
                {
                    _channel.Close();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
                _channel = null;
            }

            Logger.InfoFormat("Streaming Channel Closed for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            if (_connection != null)
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
                    Logger.Error(ex);
                }
                _connection = null;
            }

            Logger.InfoFormat("Streaming Connection Closed for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());
            }
        }
    }
}
