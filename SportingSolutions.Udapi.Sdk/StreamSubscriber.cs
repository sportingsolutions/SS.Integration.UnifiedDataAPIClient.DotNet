using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class StreamSubscriber
    {

        private static QueueingCustomConsumer _consumer;
        private int _disconnections;
        private static ConnectionFactory _connectionFactory;
        private static IConnection _connection;
        private int _maxRetries;
        private static IModel _channel;
        private static ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));
        private object _streamingLock;
        private static Dictionary<string, string> _mappingQueueToFixture;
        private static IObservable<IFixtureUpdate> _updateStream;
        private static bool _shouldStream = false;
        private static object _initSync = new object();

        static StreamSubscriber()
        {
            _mappingQueueToFixture = new Dictionary<string, string>();
        }

        private static IObservable<IFixtureUpdate> SetupStream(string fixtureId, QueueDetails queue)
        {
            lock (_initSync)
            {
                UpdateMapping(fixtureId, SetupNewBinding(queue));

                if (_updateStream == null)
                    StartStreaming();

                return _updateStream;
            }
        }

        public static void StartStream(string fixtureId,QueueDetails queue, IObserver<string> subscriber)
        {
            SetupStream(fixtureId, queue);
            _updateStream.Where(x => x.Id == fixtureId && !x.IsEcho).Select(x => x.Message).ObserveOn(Scheduler.Default).Subscribe(subscriber);
            (_updateStream as IConnectableObservable<IFixtureUpdate>).Connect();
        }

        public static void StopStream(string fixtureId)
        {
            
        }

        public static void SubscribeToEchoStream(IObserver<string> subscriber)
        {
            _updateStream.Where(x => x.IsEcho).Select(x => x.Message).ObserveOn(Scheduler.Default).Subscribe(subscriber);
            (_updateStream as IConnectableObservable<IFixtureUpdate>).Connect();
        }

        private static void StartStreaming()
        {
            _shouldStream = true;
            _updateStream = Observable.Generate(_shouldStream, x => _shouldStream, x => _shouldStream,
                                                x => GetMessage(),
                                                Scheduler.Default);
            _updateStream = _updateStream.Publish();
        }

        private static IFixtureUpdate GetMessage()
        {
            var output = _consumer.Queue.Dequeue();
            if (output == null) return null;

            var deliveryArgs = (BasicDeliverEventArgs)output;
            var message = deliveryArgs.Body;
            var fixtureStreamUpdate = new FixtureStreamUpdate() {Id = _mappingQueueToFixture[deliveryArgs.ConsumerTag]};

            

            var messageString = Encoding.UTF8.GetString(message);

            var jobject = JObject.Parse(messageString);
            if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
            {
             //   _logger.DebugFormat("Echo arrived for fixtureId={0}", fixtureStreamUpdate.Id);
                fixtureStreamUpdate.Message = jobject["Content"].Value<String>();
                fixtureStreamUpdate.IsEcho = true;
            }
            else
            {
                _logger.DebugFormat("Update arrived for fixtureId={0}", fixtureStreamUpdate.Id);
                fixtureStreamUpdate.Message = messageString;
            }
            
            return fixtureStreamUpdate;
        }

        private static void UpdateMapping(string fixtureId, string consumerTag)
        {
            _logger.DebugFormat("Mapping fixtureId={0} to consumerTag={1}",fixtureId,consumerTag);
            _mappingQueueToFixture[consumerTag] = fixtureId;
        }

        private static string SetupNewBinding(QueueDetails queue)
        {
            lock (_initSync)
            {
                if (_connection == null || _consumer == null)
                    InitializeConnection(queue);
            }

            return _channel.BasicConsume(queue.Name, true, _consumer);
        }

        //TODO: Reconnections
        private static void InitializeConnection(QueueDetails queue)
        {
            _connectionFactory = _connectionFactory ?? new ConnectionFactory();
            _connectionFactory.RequestedHeartbeat = 5;
            _connectionFactory.HostName = queue.Host;
            _connectionFactory.Port = queue.Port;
            _connectionFactory.UserName = queue.UserName;
            _connectionFactory.Password = queue.Password;
            _connectionFactory.VirtualHost = queue.VirtualHost;

            _connection = _connectionFactory.CreateConnection();

            _channel = _connection.CreateModel();
            _consumer = new QueueingCustomConsumer(_channel);
            _channel.BasicQos(0, 10, false);
        }

        public void Delete(string fixtureId)
        {

        }




    }
}
