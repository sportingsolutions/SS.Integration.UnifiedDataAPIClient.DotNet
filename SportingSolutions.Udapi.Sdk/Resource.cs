using System;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource
    {
        private bool _isStreaming;
        private bool _streamingCompleted;
        private readonly ManualResetEvent _pauseStream;

        internal Resource(NameValueCollection headers, RestItem restItem) : base(headers, restItem)
        {
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
            if(State != null)
            {
                var theLink = State.Links.First(restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/snapshot");
                
                return RestHelper.GetResponse(new Uri(theLink.Href), null, "GET", "application/json", Headers);
            }
            return "";
        }

        public void StartStreaming()
        {
            if (State != null)
            {
                Task.Factory.StartNew(stateObj =>
                {
                    var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp");
                    var amqpLink = restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

                    var amqpUri = new Uri(amqpLink.Href);
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

                    var connection = connectionFactory.CreateConnection();
                    if(StreamConnected != null)
                    {
                        StreamConnected(this, new EventArgs());    
                    }
                    
                    var channel = connection.CreateModel();
                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queueName, true, consumer);
                    channel.BasicQos(0, 10, false);

                    _isStreaming = true;
                    while (_isStreaming)
                    {
                        _pauseStream.WaitOne();
                        var output = consumer.Queue.Dequeue();
                        if (output != null)
                        {
                            var deliveryArgs = (BasicDeliverEventArgs)output;
                            var message = deliveryArgs.Body;
                            if(StreamEvent != null)
                            {
                                StreamEvent(this, new StreamEventArgs(Encoding.UTF8.GetString(message)));    
                            }
                        }
                    }

                    channel.Close();
                    connection.Close();
                    _streamingCompleted = true;
                }, null);
                
            }
        }

        public void PauseStreaming()
        {
            _pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            _pauseStream.Set();
        }

        public void StopStreaming()
        {
            _isStreaming = false;
            while(!_streamingCompleted)
            {
                
            }
            if(StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());    
            }
        }

        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
    }
}
