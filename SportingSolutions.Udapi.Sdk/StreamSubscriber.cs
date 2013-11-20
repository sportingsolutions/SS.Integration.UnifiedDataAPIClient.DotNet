using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class StreamSubscriber
    {
        private static readonly object InitSync = new object();
        private static readonly object QueueBindSync = new object();

        private static readonly ILog Logger;

        private static bool _shouldStream = false;
        private static IObservable<IMessageUpdate> _updateStream;

        private static QueueingCustomConsumer _consumer;
        private static ConnectionFactory _connectionFactory;
        private static IConnection _connection;
        private static readonly QueueDetails QueueDetails;
        private static IModel _channel;

        private static readonly ConcurrentDictionary<string, string> MappingQueueToFixture;
        private static readonly ConcurrentDictionary<string, IDisposable> Subscriptions;
        private static readonly ConcurrentDictionary<string, Resource> SubscribedResources;

        static StreamSubscriber()
        {
            Logger = LogManager.GetLogger(typeof(StreamController));
            QueueDetails = new QueueDetails();
            MappingQueueToFixture = new ConcurrentDictionary<string, string>();
            Subscriptions = new ConcurrentDictionary<string, IDisposable>();
            SubscribedResources = new ConcurrentDictionary<string, Resource>();
        }

        public static void SubscribeStream(Resource resource, IObserver<string> streamObserver)
        {
            var fixtureId = resource.Id;
            var queue = resource.GetQueueDetails();

            // Bind the queue name to the fixture id
            SetupStream(resource, queue);

            //Generate update stream with inifinite elements 
            GenerateUpdateStreamItems();

            // Subscribe observer to specific messages by fixture Id
            var subscription = _updateStream.Where(update => update != null && update.Id == fixtureId && !update.IsEcho)
                                            .Select(update => update.Message).ObserveOn(Scheduler.Default)
                                            .Subscribe(streamObserver);

            // Store the subscription (IDisposable) objects so we can stop streaming later on
            Subscriptions.AddOrUpdate(fixtureId, subscription, (s, d) => subscription);

            // Store the subscribed resources 
            SubscribedResources.AddOrUpdate(fixtureId, resource, (s, d) => resource);

            StartEmittingItems();

            //Start echos
            resource.StartEchos(queue.VirtualHost, 10000);
        }

        private static void SetupStream(Resource resource, QueueDetails queue)
        {
            lock (InitSync)
            {
                var consumerTag = SetupNewBinding(queue);

                Logger.DebugFormat("Mapping fixtureId={0} to consumerTag={1}", resource.Id, consumerTag);

                MappingQueueToFixture.AddOrUpdate(consumerTag, s => resource.Id, (s, s1) => resource.Id);
            }
        }

        private static string SetupNewBinding(QueueDetails queue)
        {
            if (_connection == null || _consumer == null)
            {
                lock (InitSync)
                {
                    if (_connection == null || _consumer == null)
                    {
                        QueueDetails.Host = queue.Host;
                        QueueDetails.Port = queue.Port;
                        QueueDetails.UserName = queue.UserName;
                        QueueDetails.Password = queue.Password;
                        QueueDetails.VirtualHost = queue.VirtualHost;

                        InitializeConnection();
                    }
                }
            }

            string consumerTag;

            lock (QueueBindSync)
            {
                consumerTag = _channel.BasicConsume(queue.Name, true, _consumer);  // BasicConsume is not thread safe
            }

            return consumerTag;
        }

        private static void InitializeConnection()
        {
            if (null == _connectionFactory)
            {
                _connectionFactory = new ConnectionFactory();
                _connectionFactory.RequestedHeartbeat = 5;
                _connectionFactory.HostName = QueueDetails.Host;
                _connectionFactory.Port = QueueDetails.Port;
                _connectionFactory.UserName = QueueDetails.UserName;
                _connectionFactory.Password = QueueDetails.Password;
                _connectionFactory.VirtualHost = QueueDetails.VirtualHost;
            }

            _connection = _connectionFactory.CreateConnection();

            _channel = StreamController.Instance.GetStreamChannel(QueueDetails.Host, QueueDetails.Port, QueueDetails.UserName, QueueDetails.Password, QueueDetails.VirtualHost);
            _consumer = new QueueingCustomConsumer(_channel);
            _channel.BasicQos(0, 10, false);
        }

        private static void GenerateUpdateStreamItems()
        {
            if (_updateStream == null)
            {
                _shouldStream = true;
                _updateStream = Observable.Generate(_shouldStream, b => _shouldStream, b => _shouldStream, b => GetMessage(),
                                                    Scheduler.Default);
                _updateStream = _updateStream.Publish();
            }
        }

        private static void StartEmittingItems()
        {
            // Connect the subscriber
            var connectableObservable = _updateStream as IConnectableObservable<IMessageUpdate>;

            if (connectableObservable != null)
            {
                connectableObservable.Connect();
            }
        }

        private static IMessageUpdate GetMessage()
        {
            var fixtureId = string.Empty;
            MessageUpdate streamMessageUpdate = null;

            while (streamMessageUpdate == null)
            {
                try
                {
                    var output = _consumer.Queue.Dequeue();

                    var message = ExtractMessage(output, ref fixtureId, _consumer);
                    streamMessageUpdate = new MessageUpdate { Id = fixtureId };

                    var jobject = JObject.Parse(message);

                    if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
                    {
                        streamMessageUpdate.Message = jobject["Content"].Value<String>();
                        streamMessageUpdate.IsEcho = true;
                        //IncrementPerformanceCounter.IncreaseCounter("Udapi SDK Received Echos");
                    }

                    else
                    {
                        Logger.DebugFormat("Update arrived for fixtureId={0}", fixtureId);
                        streamMessageUpdate.Message = message;
                    }
                }
                catch (EndOfStreamException ex)
                {
                    //HandleIndividualConnectionIssues(ex);
                }
                catch (BrokerUnreachableException ex)
                {
                    //HandleUnreachableServerIssue(ex);
                }
                catch (Exception ex)
                {
                    if (!_consumer.IsRunning)
                    {
                        //HandleIndividualConnectionIssues(ex);
                    }
                    else
                    {
                        Logger.Error(string.Format("Error processing message from Streaming Queue for fixtureId={0}", fixtureId), ex);
                    }
                }
            }

            return streamMessageUpdate;          
        }

        private static string ExtractMessage(object output, ref string fixtureId, QueueingCustomConsumer consumer)
        {
            var deliveryArgs = (BasicDeliverEventArgs)output;
            var message = deliveryArgs.Body;

            fixtureId = MappingQueueToFixture[consumer.ConsumerTag];

            return Encoding.UTF8.GetString(message);
        }

        public static void StopStream(string fixtureId)
        {
            IDisposable subscription;

            if (Subscriptions.TryRemove(fixtureId, out subscription))
            {
                subscription.Dispose();
            }

            Resource resource;

            SubscribedResources.TryRemove(fixtureId, out resource);

            resource.StopEcho();
        }
    }
}
