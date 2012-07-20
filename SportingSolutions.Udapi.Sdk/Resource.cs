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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource, IDisposable
    {
        private ILog _logger = LogManager.GetLogger(typeof(Resource).ToString());

        private bool _isStreaming;
        private readonly ManualResetEvent _pauseStream;

        private IModel _channel;
        private IConnection _connection;
        private QueueingCustomConsumer _consumer;
        private int _currentSequence;
        private Timer _sequenceCheckerTimer;

        private int _sequenceDiscrepencyThreshold;
        private int _sequenceCheckerInterval;

        private int _disconnections;
        private int _maxRetries;
        private ConnectionFactory _connectionFactory;
        private string _queueName;
        private bool _isReconnecting;


        internal Resource(NameValueCollection headers, RestItem restItem)
            : base(headers, restItem)
        {
            _logger.DebugFormat("Instantiated Resource {0}", restItem.Name);
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

        public Summary Content
        {
            get { return State.Content; }
        }

        public string GetSnapshot()
        {
            _logger.InfoFormat("Get Snapshot for  {0}", Name);
            return FindRelationAndFollowAsString("http://api.sportingsolutions.com/rels/snapshot");
        }

        public void StartStreaming()
        {
            StartStreaming(10000,2);
        }

        public void StartStreaming(int sequenceCheckerInterval, int sequenceDiscrepencyThreshold)
        {
            _logger.InfoFormat("Starting stream for {0} with Sequence Checker Interval of {1} and Discrepency Interval of {2}",Name, sequenceCheckerInterval, sequenceDiscrepencyThreshold);
            _sequenceCheckerInterval = sequenceCheckerInterval;
            _sequenceDiscrepencyThreshold = sequenceDiscrepencyThreshold;

            if (State != null)
            {
                Task.Factory.StartNew(StreamData);
            }
        }

        private void CheckSequence()
        {
            var sequence = GetSequenceAsInt();
            var sequenceGap = Math.Abs(sequence - _currentSequence);
            _logger.DebugFormat("Sequence Discrepency = {0}",sequenceGap);
            if (sequenceGap > _sequenceDiscrepencyThreshold)
            {
                if (StreamSynchronizationError != null)
                {
                    if (_sequenceCheckerTimer != null)
                    {
                        _sequenceCheckerTimer.Dispose();
                        _sequenceCheckerTimer = null;
                    }

                    StreamSynchronizationError(this, new EventArgs());
                    _isReconnecting = true;
                    Reconnect();
                    _isReconnecting = false;
                }
            }
        }

        private int GetSequenceAsInt()
        {
            try
            {
                var stringSequence = FindRelationAndFollowAsString("http://api.sportingsolutions.com/rels/sequence");
                if (!string.IsNullOrEmpty(stringSequence))
                {
                    var sequence = Convert.ToInt32(stringSequence);
                    return sequence;
                }
            }
            catch(Exception ex)
            {
                _logger.Error("Unable to retrieve Sequence", ex);
            }
            return 0;
        }

        private void StreamData()
        {
            _connectionFactory = new ConnectionFactory();

            _maxRetries = 10;
            _disconnections = 0;

            _isStreaming = true;

            Reconnect();
            
            _logger.InfoFormat("Initialised connection to Streaming Queue for {0}", Name);

            
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
                            var messageString = Encoding.UTF8.GetString(message);
                            var jobject = JObject.Parse(messageString);
                            _currentSequence = jobject["Content"]["Sequence"].Value<int>();
                            StreamEvent(this, new StreamEventArgs(messageString));    
                        }
                    }
                    _disconnections = 0;
                }
                catch(Exception)
                {
                    _logger.WarnFormat("Lost connection to stream {0}", Name);
                    //connection lost
                    if(!_isReconnecting)
                    {
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
            _logger.WarnFormat("Attempting to reconnect stream for {0}, Attempt {1}",Name,_disconnections+1);
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
                        var virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);
                        _connectionFactory.VirtualHost = "/" + virtualHost;
                    }

                    if (_channel != null)
                    {
                        _channel.Close();
                        _channel = null;
                    }

                    if (_connection != null)
                    {
                        _connection.Close();
                        _connection = null;
                    }

                    _connection = _connectionFactory.CreateConnection();
                    _logger.InfoFormat("Successfully connected to Streaming Server for {0}", Name);

                    if(_sequenceCheckerTimer != null)
                    {
                        _sequenceCheckerTimer.Dispose();
                        _sequenceCheckerTimer = null;
                    }
                    _currentSequence = GetSequenceAsInt();
                    _sequenceCheckerTimer = new Timer(timerAutoEvent => CheckSequence(), null, _sequenceCheckerInterval, _sequenceCheckerInterval);

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
                catch (BrokerUnreachableException)
                {
                    if (_disconnections > _maxRetries)
                    {
                        _logger.ErrorFormat("Failed to reconnect Stream for {0} ",Name);
                        StopStreaming();
                        throw;
                    }
                    // give time to load balancer to notice the node is down
                    Thread.Sleep(500);
                    _disconnections++;
                    _logger.WarnFormat("Failed to reconnect stream {0}, Attempt {1}", Name,_disconnections);   
                }
                catch (Exception)
                {
                    StopStreaming();
                    break;
                }
            }
        }
        
        public void PauseStreaming()
        {
            _logger.InfoFormat("Streaming paused for {0}",Name);
            _pauseStream.Reset();
            if(_sequenceCheckerTimer != null)
            {
                _sequenceCheckerTimer.Change(Timeout.Infinite, Timeout.Infinite);    
            }
        }

        public void UnPauseStreaming()
        {
            _logger.InfoFormat("Streaming unpaused for {0}", Name);
            _pauseStream.Set();
            if(_sequenceCheckerTimer != null)
            {
                _sequenceCheckerTimer.Change(_sequenceCheckerInterval, _sequenceCheckerInterval);    
            }
        }

        public void StopStreaming()
        {
            _isStreaming = false;
            if (_consumer != null)
            {
                _channel.BasicCancel(_consumer.ConsumerTag);
                if(_sequenceCheckerTimer != null)
                {
                    _sequenceCheckerTimer.Dispose();
                    _sequenceCheckerTimer = null;
                }
            }
        }

        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
        public event EventHandler StreamSynchronizationError;

        public void Dispose()
        {
            _logger.InfoFormat("Streaming stopped for {0}", Name);
            if(_channel != null)
            {
                _channel.Close();
                _channel = null;
            }

            if(_connection != null)
            {
                _connection.Close();
                _connection = null;
            }

            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());
            }
        }
    }
}
