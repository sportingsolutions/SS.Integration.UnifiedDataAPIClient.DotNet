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
                SetupNewBinding(queue);
                UpdateMapping(fixtureId, queue);

                if (_updateStream == null)
                    StartStreaming();

                //return _updateStream.Where(x => x.Id == fixtureId).Select(x => x.Message);
                
                return _updateStream;
            }
        }

        public static void StartStream(string fixtureId,QueueDetails queue, IObserver<string> subscriber)
        {
            SetupStream(fixtureId, queue);
            _updateStream.Where(x => x.Id == fixtureId).Select(x=> x.Message).Subscribe(subscriber);
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
            var headers = deliveryArgs.BasicProperties.Headers;
            var message = deliveryArgs.Body;

            //LastMessageReceived = DateTime.UtcNow;

            var fixtureStreamUpdate = new FixtureStreamUpdate() { Id = _mappingQueueToFixture.Where(x=> x.Value == deliveryArgs.RoutingKey.Split('.')[2]).First().Value };

            var messageString = Encoding.UTF8.GetString(message);

            _logger.DebugFormat("Update arrived for fixtureId={0}",fixtureStreamUpdate.Id);

            var jobject = JObject.Parse(messageString);
            if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
            {
                var split = jobject["Content"].Value<String>().Split(';');
                //_lastRecievedEchoGuid = split[0];
                var timeSent = DateTime.ParseExact(split[1], "yyyy-MM-ddTHH:mm:ss.fffZ",
                                                   CultureInfo.InvariantCulture);
                var roundTripTime = DateTime.Now - timeSent;

                var roundMillis = roundTripTime.TotalMilliseconds;

                //EchoRoundTripInMilliseconds = roundMillis;

                fixtureStreamUpdate.IsEcho = true;

                //_echoResetEvent.Set();
            }
            else
            {
                fixtureStreamUpdate.Message = messageString;
            }

            return fixtureStreamUpdate;
        }

        //protected double EchoRoundTripInMilliseconds
        //{
        //    get { throw new NotImplementedException(); }
        //    set { throw new NotImplementedException(); }
        //}

        private static void UpdateMapping(string fixtureId, QueueDetails queue)
        {
            _logger.DebugFormat("Mapping fixtureId={0} to queue={1}",fixtureId,queue.Name);
            _mappingQueueToFixture[queue.Name] = fixtureId;
        }

        private static void SetupNewBinding(QueueDetails queue)
        {
            lock (_initSync)
            {
                if (_connection == null || _consumer == null)
                    InitializeConnection(queue);
            }
            _channel.BasicConsume(queue.Name, true, _consumer);
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
