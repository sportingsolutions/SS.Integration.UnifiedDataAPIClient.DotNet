using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;
using log4net.Repository.Hierarchy;

namespace SportingSolutions.Udapi.Sdk
{
    internal class ResourceSingleQueue : Endpoint, IResource, IDisposable, IStreamStatistics
    {
        private ILog _logger = LogManager.GetLogger(typeof(ResourceSingleQueue));

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
        public bool IsStreamActive { get; private set; }
        public double EchoRoundTripInMilliseconds { get; private set; }
        private static QueueingCustomConsumer _consumer;
        private int _disconnections;
        private ConnectionFactory _connectionFactory;
        private static IConnection _connection;
        private int _maxRetries;
        private static IModel _channel;


        internal ResourceSingleQueue(NameValueCollection headers, RestItem restItem)
            : base(headers, restItem)
        {

        }

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
            _streamObserver = Observer.Create<string>(x =>
                {
                    _logger.DebugFormat("Stream update arrived to a resource with Id={0}!", this.Id);
                    if (StreamEvent != null)
                    {
                        StreamEvent(this, new StreamEventArgs(x));
                    }
                });

            var queueDetails = GetQueueDetails();
            StreamSubscriber.StartStream(Id,queueDetails, _streamObserver);
            EchoSender.StartEcho(PostEcho, queueDetails);
        }

        private void PostEcho(StreamEcho x)
        {
            var theLink =
                State.Links.First(
                    restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/stream/echo");
            var theUrl = theLink.Href;

            var stringStreamEcho = x.ToJson();

            RestHelper.GetResponse(new Uri(theUrl), stringStreamEcho, "POST", "application/json",
                                   Headers, 3000);
        }

        public IObservable<string> GetStreamData()
        {
            return null;
        }

        private void StreamData()
        {
            Reconnect();



            while (true)
            {
                try
                {
                    var output = _consumer.Queue.Dequeue();
                    if (output == null) continue;

                    var deliveryArgs = (BasicDeliverEventArgs)output;
                    var message = deliveryArgs.Body;
                    if (StreamEvent == null) continue;

                    LastMessageReceived = DateTime.UtcNow;

                    var messageString = Encoding.UTF8.GetString(message);
                    var jobject = JObject.Parse(messageString);
                    if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
                    {
                        var split = jobject["Content"].Value<String>().Split(';');
                        //_lastRecievedEchoGuid = split[0];
                        var timeSent = DateTime.ParseExact(split[1], "yyyy-MM-ddTHH:mm:ss.fffZ",
                                                           CultureInfo.InvariantCulture);
                        var roundTripTime = DateTime.Now - timeSent;

                        var roundMillis = roundTripTime.TotalMilliseconds;

                        EchoRoundTripInMilliseconds = roundMillis;
                        //_echoResetEvent.Set();
                    }
                    else
                    {
                        //_isProcessingStreamEvent = true;      
                        StreamEvent(this, new StreamEventArgs(messageString));
                        //_isProcessingStreamEvent = false;
                    }

                    _disconnections = 0;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);

                }
            }
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            StartStreaming();
        }

        public void PauseStreaming()
        {

        }

        public void UnPauseStreaming()
        {

        }

        public void StopStreaming()
        {

        }

        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
        public event EventHandler StreamSynchronizationError;



        private static object _streamingLock = new object();
        private Task _streamingTask;
        private IObserver<string> _streamObserver;

        private QueueDetails GetQueueDetails()
        {
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp");
            var amqpLink =
                restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

            var amqpUri = new Uri(amqpLink.Href);

            var queueDetails = new QueueDetails() { Host = amqpUri.Host };

            var userInfo = amqpUri.UserInfo;
            userInfo = HttpUtility.UrlDecode(userInfo);
            if (!String.IsNullOrEmpty(userInfo))
            {
                var userPass = userInfo.Split(':');
                if (userPass.Length > 2)
                {
                    throw new ArgumentException(string.Format("Bad user info in AMQP URI: {0}", userInfo));
                }
                queueDetails.UserName = userPass[0];
                if (userPass.Length == 2)
                {
                    queueDetails.Password = userPass[1];
                }
            }

            var path = amqpUri.AbsolutePath;
            if (!String.IsNullOrEmpty(path))
            {
                queueDetails.Name = path.Substring(path.IndexOf('/', 1) + 1);
                var virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);

                queueDetails.VirtualHost = "/" + virtualHost;
            }

            var port = amqpUri.Port;
            if (port != -1)
            {
                queueDetails.Port = port;
            }

            return queueDetails;
        }



        private void Reconnect()
        {
            lock (_streamingLock)
            {
                _logger.WarnFormat("Attempting to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}",
                                   Name, Id, _disconnections + 1);
                var success = false;

                try
                {
                    var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp");
                    var amqpLink =
                        restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

                    var amqpUri = new Uri(amqpLink.Href);

                    if (_connectionFactory == null)
                    {
                        _connectionFactory = _connectionFactory ?? new ConnectionFactory();
                        _connectionFactory.RequestedHeartbeat = 5;
                    }


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
                    string queueName = "";
                    var path = amqpUri.AbsolutePath;
                    if (!String.IsNullOrEmpty(path))
                    {
                        queueName = path.Substring(path.IndexOf('/', 1) + 1);
                        var virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);

                        _connectionFactory.VirtualHost = "/" + virtualHost;
                    }

                    //if (_channel != null)
                    //{
                    //    _channel.Close();
                    //    _channel = null;
                    //}

                    //try
                    //{
                    //    if (_connection != null)
                    //    {
                    //        if (_connection.IsOpen)
                    //        {
                    //            _connection.Close();
                    //        }
                    //        _connection = null;
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    _logger.Error(ex);
                    //}

                    if (_connection == null)
                        _connection = _connectionFactory.CreateConnection();

                    _logger.InfoFormat(
                            "Successfully connected to Streaming Server for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

                    //StartEcho();

                    if (StreamConnected != null)
                    {
                        StreamConnected(this, new EventArgs());
                    }

                    if (_channel == null)
                    {
                        _channel = _connection.CreateModel();
                        _consumer = new QueueingCustomConsumer(_channel);
                        _channel.BasicQos(0, 10, false);
                    }

                    _channel.BasicConsume(queueName, true, _consumer);

                    success = true;
                }
                catch (Exception)
                {
                    if (_disconnections > _maxRetries)
                    {
                        _logger.ErrorFormat("Failed to reconnect Stream for fixtureName=\"{0}\" fixtureId={1} ", Name,
                                            Id);
                        StopStreaming();
                    }
                    else
                    {
                        // give time for load balancer to notice the node is down
                        Thread.Sleep(500);
                        _disconnections++;
                        _logger.WarnFormat(
                            "Failed to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}", Name, Id,
                            _disconnections);
                    }
                }
            }

        }

        public void Dispose()
        {
            _logger.InfoFormat("Streaming stopped for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            if (_channel != null)
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
                    _logger.Error(ex);
                }
                _connection = null;
            }

            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());
            }
        }
    }
}
