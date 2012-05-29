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
            if (State != null)
            {
                _logger.InfoFormat("Get Snapshot for  {0}", Name);
                var theLink = State.Links.First(restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/snapshot");

                return RestHelper.GetResponse(new Uri(theLink.Href), null, "GET", "application/json", Headers);
            }
            return "";
        }

        public void StartStreaming()
        {
            _logger.InfoFormat("Starting stream for {0}",Name);
            if (State != null)
            {
                Task.Factory.StartNew(StreamData); 
            }
        }

        private void StreamData()
        {
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp");
            var amqpLink = restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

            var amqpUri = new Uri(amqpLink.Href);
            Console.WriteLine("Host: {0}", amqpUri);
            var connectionFactory = new ConnectionFactory();

            var host = amqpUri.Host;
            if (!String.IsNullOrEmpty(host))
            {
                connectionFactory.HostName = host;
            }
            var port = amqpUri.Port;
            if (port != -1)
            {
                connectionFactory.Port = port;
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
                connectionFactory.UserName = userPass[0];
                if (userPass.Length == 2)
                {
                    connectionFactory.Password = userPass[1];
                }
            }
            var queueName = "";
            var path = amqpUri.AbsolutePath;
            if (!String.IsNullOrEmpty(path))
            {
                queueName = path.Substring(path.IndexOf('/', 1) + 1);
                var virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);
                connectionFactory.VirtualHost = "/" + virtualHost;
            }

            _connection = connectionFactory.CreateConnection();
            _logger.InfoFormat("Successfully connected to Streaming Server for {0}",Name);

            if (StreamConnected != null)
            {
                StreamConnected(this, new EventArgs());
            }

            _channel = _connection.CreateModel();
            var consumer = new QueueingCustomConsumer(_channel);

            _channel.BasicConsume(queueName, true, consumer);
            _channel.BasicQos(0, 10, false);
            _logger.InfoFormat("Initialised connection to Streaming Queue for {0}", Name);

            _isStreaming = true;

            int maxRetries = 10;
            int disconnections = 0;

            Action reconnect = () =>
                                   {
                                       _logger.WarnFormat("Attempting to reconnect stream for {0}, Attempt {1}",Name,disconnections+1);
                                       var success = false;
                                       while (!success && _isStreaming)
                                       {
                                           try
                                           {
                                               _connection = connectionFactory.CreateConnection();
                                               _channel = _connection.CreateModel();
                                               consumer = new QueueingCustomConsumer(_channel);
                                               _channel.BasicConsume(queueName, true, consumer);
                                               _channel.BasicQos(0, 10, false);
                                               success = true;
                                           }
                                           catch (BrokerUnreachableException)
                                           {
                                               if (disconnections > maxRetries)
                                               {
                                                   _logger.ErrorFormat("Failed to reconnect Stream for {0} ",Name);
                                                   StopStreaming();
                                                   throw;
                                               }
                                               // give time to load balancer to notice the node is down
                                               Thread.Sleep(500);
                                               disconnections++;
                                               _logger.WarnFormat("Failed to reconnect stream {0}, Attempt {1}", Name,disconnections);   
                                           }
                                           catch (Exception)
                                           {
                                               StopStreaming();
                                               break;
                                           }
                                       }
                                   };

            consumer.QueueCancelled += reconnect;

            while (_isStreaming)
            {
                try
                {
                    _pauseStream.WaitOne();
                    var output = consumer.Queue.Dequeue();
                    if (output != null)
                    {
                        var deliveryArgs = (BasicDeliverEventArgs)output;
                        var message = deliveryArgs.Body;
                        if (StreamEvent != null)
                        {
                            StreamEvent(this, new StreamEventArgs(Encoding.UTF8.GetString(message)));
                        }
                    }
                    disconnections = 0;
                }
                //catch (EndOfStreamException)
                catch(Exception)
                {
                    _logger.WarnFormat("Lost connection to stream {0}", Name);
                    //connection lost
                    reconnect();
                }
            }
        }
        
        public void PauseStreaming()
        {
            _logger.InfoFormat("Streaming paused for {0}",Name);
            _pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            _logger.InfoFormat("Streaming unpaused for {0}", Name);
            _pauseStream.Set();
        }

        public void StopStreaming()
        {
            _logger.InfoFormat("Streaming stopped for {0}", Name);
            Dispose();
        }

        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;

        public void Dispose()
        {
            _isStreaming = false;

            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());
            }

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
        }
    }
}
