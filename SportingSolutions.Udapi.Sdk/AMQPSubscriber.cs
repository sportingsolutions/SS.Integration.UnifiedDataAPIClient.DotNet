using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public sealed class AmqpSubscriber
    {
        private readonly ILog _logger;
        private static volatile AmqpSubscriber _instance;
        private static readonly object SyncRoot = new Object();
        private static readonly object InitSync = new object();
        private static readonly object QueueBindSync = new object();

        private readonly ConcurrentDictionary<string, string> _mappingQueueToFixture;
        private readonly ConcurrentDictionary<string, IDisposable> _subscriptions;
        private readonly ConcurrentDictionary<string, ResourceSingleQueue> _subscribedResources;

        private IObservable<IFixtureUpdate> _updateStream;

        private bool _shouldStream;

        private string _hostName;
        private int _port;
        private string _userName;
        private string _password;
        private string _virtualHost;

        private QueueingCustomConsumer _consumer;
        private ConnectionFactory _connectionFactory;
        private IConnection _connection;
        private IModel _channel;

        private IConnectClient _connectClient;

        private AmqpSubscriber()
        {
            _logger = LogManager.GetLogger(typeof (AmqpSubscriber));

            _mappingQueueToFixture = new ConcurrentDictionary<string, string>();
            _subscriptions = new ConcurrentDictionary<string, IDisposable>();
            _subscribedResources = new ConcurrentDictionary<string, ResourceSingleQueue>();
        }

        public static AmqpSubscriber Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new AmqpSubscriber();
                        }
                    }
                }
                return _instance;
            }
        }

        private Uri _echoUri;
        public Uri EchoUri
        {
            get { return _echoUri; }
            set
            {
                if (_echoUri == null)
                {
                    _echoUri = value;
                }
            }
        }

        private void UpdateMapping(string fixtureId, string consumerTag)
        {
            _logger.DebugFormat("Mapping fixtureId={0} to consumerTag={1}", fixtureId, consumerTag);

            _mappingQueueToFixture.AddOrUpdate(consumerTag, s => fixtureId, (s, s1) => fixtureId);
        }

        private void SetupStream(string fixtureId, QueueDetails queue)
        {
            lock (InitSync)
            {
                UpdateMapping(fixtureId, SetupNewBinding(queue));

                if (_updateStream == null)
                {
                    StartStreaming();
                }
            }
        }

        private string SetupNewBinding(QueueDetails queue)
        {
            if (_connection == null || _consumer == null)
            {
                lock (InitSync)
                {
                    if (_connection == null || _consumer == null)
                    {
                        _hostName = queue.Host;
                        _port = queue.Port;
                        _userName = queue.UserName;
                        _password = queue.Password;
                        _virtualHost = queue.VirtualHost;

                        InitializeConnection();
                    }
                }
            }

            return BindQueueToConnection(queue.Name);
        }

        private void InitializeConnection()
        {
            if (null == _connectionFactory)
            {
                _connectionFactory = new ConnectionFactory
                    {
                        RequestedHeartbeat = 5,
                        HostName = _hostName,
                        Port = _port,
                        UserName = _userName,
                        Password = _password,
                        VirtualHost = _virtualHost
                    };
            }

            _connection = _connectionFactory.CreateConnection();

            _channel = _connection.CreateModel();
            _consumer = new QueueingCustomConsumer(_channel);
            _channel.BasicQos(0, 10, false);
        }

        private void CloseConnection()
        {
            _logger.Debug("Closing Stream Server connection");

            if (_channel != null)
            {
                _channel.Dispose();
                _channel = null;
            }

            if (_connection != null)
            {
                try
                {
                    _connection.Dispose();
                }
                catch (Exception ex)
                {
                }

                _connection = null;
            }
        }

        private string BindQueueToConnection(string queueName)
        {
            string consumerTag;

            lock (QueueBindSync)
            {
                consumerTag = _channel.BasicConsume(queueName, true, _consumer);  // BasicConsume is not thread safe
            }

            return consumerTag;
        }

        public IResource CreateResource(RestItem resourceRestItem, IConnectClient connectClient, Uri echoUri)
        {
            if (_connectClient == null)
            {
                _connectClient = connectClient;
            }

            EchoUri = echoUri;
            var resource = new ResourceSingleQueue(resourceRestItem, connectClient, this);
            _subscribedResources.AddOrUpdate(resource.Id, resource, (x, y) => resource);
            return resource;
        }

        public void StartStream(ResourceSingleQueue resource, IObserver<string> subscriber)
        {
            var fixtureId = resource.Id;
            var queue = resource.GetQueueDetails();

            // Bind the queue name to the fixture id
            SetupStream(fixtureId, queue);

            // Subscribe observer to specific messages by fixture Id
            var subscription = _updateStream.Where(x => x != null && x.Id == fixtureId && !x.IsEcho).Select(x => x.Message).ObserveOn(Scheduler.Default).Subscribe(subscriber);

            // Store the subscription (IDisposable) objects so we can stop streaming later on
            _subscriptions.AddOrUpdate(fixtureId, subscription, (s, d) => subscription);

            // Store the subscribed resources 
        //    _subscribedResources.AddOrUpdate(fixtureId, resource, (s, d) => resource);

            // Connect the subscriber
            (_updateStream as IConnectableObservable<IFixtureUpdate>).Connect();
        }

        private void StartStreaming()
        {
            _shouldStream = true;
            _updateStream = Observable.Generate(_shouldStream, x => _shouldStream, x => _shouldStream,
                                                x => GetMessage(),
                                                Scheduler.Default);
            _updateStream = _updateStream.Publish();
            var qd = new QueueDetails
                {
                    Host = _hostName,
                    Password = _password,
                    UserName = _userName,
                    Port = _port,
                    VirtualHost = _virtualHost
                };
            EchoSender.StartEcho(PostEcho, qd);
        }

        private string ExtractMessage(object output, ref string fixtureId)
        {
            var deliveryArgs = (BasicDeliverEventArgs)output;
            var message = deliveryArgs.Body;

            fixtureId = _mappingQueueToFixture[_consumer.ConsumerTag];

            return Encoding.UTF8.GetString(message);
        }

        private void PostEcho(StreamEcho streamEcho)
        {
            var stopwatch = new Stopwatch();
            Action<IRestResponse<object>> requestCallback = delegate(IRestResponse<object> response)
            {
                _logger.DebugFormat("Post Echo took duration={0}ms", stopwatch.ElapsedMilliseconds);
                if (response.ErrorException != null)
                {
                    RestErrorHelper.LogRestError(_logger, response, "Echo Http Error");
                    return;
                }
                _logger.InfoFormat("Echo has been sent successfuly");
            };
            _logger.Info("Echo about to be sent");
            _connectClient.RequestAsync(EchoUri, Method.POST, streamEcho, requestCallback);
        }

        private IFixtureUpdate GetMessage()
        {
            var fixtureId = string.Empty;
            FixtureStreamUpdate fixtureStreamUpdate = null;

            while (fixtureStreamUpdate == null)
            {
                try
                {
                    var output = _consumer.Queue.Dequeue();
                    if (output == null) return null;

                    var message = ExtractMessage(output, ref fixtureId);

                    fixtureStreamUpdate = new FixtureStreamUpdate() { Id = fixtureId };
                    var jobject = JObject.Parse(message);

                    if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
                    {
                        fixtureStreamUpdate.Message = jobject["Content"].Value<String>();
                        fixtureStreamUpdate.IsEcho = true;
                    }
                    else
                    {
                        _logger.DebugFormat("Update arrived for fixtureId={0}", fixtureId);
                        fixtureStreamUpdate.Message = message;
                    }
                }
                catch (EndOfStreamException ex)
                {
                    HandleIndividualConnectionIssues(ex);
                }
                catch (BrokerUnreachableException ex)
                {
                    HandleUnreachableServerIssue(ex);
                }
                catch (Exception ex)
                {
                    if (!_consumer.IsRunning)
                    {
                        HandleIndividualConnectionIssues(ex);
                    }
                    else
                    {
                        _logger.Error(string.Format("Error processing message from Streaming Queue for fixtureId={0}", fixtureId), ex);
                    }
                }
            }

            return fixtureStreamUpdate;
        }

        /// <summary>
        /// Ensure we don't miss any update by comparing sequence numbers
        /// </summary>
        private void CheckForMissingUpdates(string fixtureId, ResourceSingleQueue resource)
        {
            Task.Factory.StartNew(
                () =>
                {
                    _logger.DebugFormat("Retrieving snapshot to check whether an update was lost after disconnection for fixtureId={0}", fixtureId);

                    var snapshot = resource.GetSnapshot();
                    var updateWrapper = "{\"Relation\":\"http://api.sportingsolutions.com/rels/snapshot\",\"Content\":";
                    var update = string.Format("{0}{1}{2}", updateWrapper, snapshot, "}");

                    var sequence = ResourceSingleQueue.GetSequenceFromStreamUpdate(update);

                    _logger.DebugFormat("Sequence={0} found in snapshot for fixtureId={1} which last sequence={2}", sequence, fixtureId, resource.LastSequence);

                    if (sequence > resource.LastSequence)
                    {
                        _logger.InfoFormat("Sending snapshot as an update due to a missing update has been identified for fixtureId={0}", fixtureId);

                        resource.PushValueToObserver(update);
                    }
                });
        }

        private void HandleIndividualConnectionIssues(Exception exception)
        {
            var consumerTag = _consumer.ConsumerTag;
            var fixtureId = _mappingQueueToFixture[consumerTag];
            var resource = _subscribedResources[fixtureId];

            _logger.Error(string.Format("Lost connection to Streaming Server for fixtureId={0}", fixtureId), exception);

            StopEchos();

            Thread.Sleep(1000);

            Reconnect(resource);

            CheckForMissingUpdates(fixtureId, resource);
        }

        private void HandleUnreachableServerIssue(Exception exception)
        {
            _logger.Error(string.Format("Cannot reach Streaming Server"), exception);

            StopEchos();

            Thread.Sleep(5000);

            Reconnect();

            //start echos
        }

        /// <summary>
        /// Reconnect to Streaming Server
        /// Rebind all subscribers' queues and fire all resources' events (if singleResource is null)
        /// If singleResource param is not null, will reconnect all queues anyway but only its events will be fired
        /// </summary>
        private void Reconnect(ResourceSingleQueue singleResource = null)
        {
            var success = false;
            var disconnections = 1;

            while (!success)
            {
                try
                {
                    if (disconnections == 2)  // It'll wait for second attempt to fire disconnection events
                    {
                        FireDisconnectedEvent(singleResource);
                    }

                    _logger.WarnFormat("Attempting to reconnect to Streaming Server. Attempt {0}", disconnections);

                    // Rebuild RabbitMQ connection
                    InitializeConnection();

                    // Bind all existing subscribers' queues to new connection and start Echo
                    RebindAllSubscribersQueues();

                    _logger.InfoFormat("Successfully connected to Streaming Server");

                    if (disconnections > 1)  // It'll fire connected events only after first attempt
                    {
                        FireConnectedEvent(singleResource);
                    }

                    success = true;
                    disconnections++;
                }
                catch (Exception ex)
                {
                    _logger.Error("Error while trying to reconnect to the Streaming Server", ex);

                    // Give time for load balancer to notice the node is down
                    Thread.Sleep(500);
                }
            }
        }

        private void FireConnectedEvent(ResourceSingleQueue singleResource = null)
        {
            if (singleResource != null)
            {
                Task.Factory.StartNew(singleResource.FireStreamConnected);
            }
            else
            {
                foreach (var subscribedResource in _subscribedResources)
                {
                    var resource = subscribedResource.Value;

                    if (resource != null && resource.IsStreamActive)
                    {
                        Task.Factory.StartNew(resource.FireStreamConnected);
                    }
                }
            }
        }

        private void RebindAllSubscribersQueues()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            Parallel.ForEach(_subscribedResources, options,
                subscribedResource =>
                {
                    var resource = subscribedResource.Value;

                    if (resource != null && resource.IsStreamActive)
                    {
                        var queueName = resource.GetQueueDetails().Name;

                        var consumerTag = BindQueueToConnection(queueName);

                        UpdateMapping(resource.Id, consumerTag);
                    }
                });
        }

        private void StopEchos()
        {
            foreach (var subscribedResource in _subscribedResources)
            {
                var resource = subscribedResource.Value;

                if (resource != null)
                {
                    StopEcho(resource);
                }
            }
        }

        private void StopEcho(string fixtureId)
        {
            EchoSender.StopEcho();
        }

        private void StopEcho(ResourceSingleQueue resource)
        {
            EchoSender.StopEcho();
        }

        private void FireDisconnectedEvent(ResourceSingleQueue singleResource = null)
        {
            if (singleResource != null)
            {
                Task.Factory.StartNew(singleResource.FireStreamDisconnected);
            }
            else
            {
                foreach (var subscribedResource in _subscribedResources)
                {
                    var resource = subscribedResource.Value;

                    if (resource != null && resource.IsStreamActive)
                    {
                        Task.Factory.StartNew(resource.FireStreamDisconnected);
                    }
                }
            }
        }

        /// <summary>
        /// Unsubscribe a resource from the source
        /// </summary>
        public void StopStream(string fixtureId)
        {
            IDisposable subscription;

            if (_subscriptions.TryRemove(fixtureId, out subscription))
            {
                subscription.Dispose();
            }

            //ResourceSingleQueue resource;

            //_subscribedResources.TryRemove(fixtureId, out resource);

        }

        /// <summary>
        /// Unsubscribe all resources and close RabbitMq connection
        /// </summary>
        public void StopStream()
        {
            lock (InitSync)
            {
                _shouldStream = false;

                foreach (var subscribedResource in _subscribedResources)
                {
                    if (subscribedResource.Value.IsStreamActive)
                    {
                        StopStream(subscribedResource.Key);
                    }
                }

                CloseConnection();
            }
        }

        public void SubscribeToEchoStream(IObserver<string> subscriber)
        {
            _updateStream.Where(x => x != null && x.IsEcho).Select(x => x.Message).ObserveOn(Scheduler.Default).Subscribe(subscriber);

            (_updateStream as IConnectableObservable<IFixtureUpdate>).Connect();
        }
    }
}
