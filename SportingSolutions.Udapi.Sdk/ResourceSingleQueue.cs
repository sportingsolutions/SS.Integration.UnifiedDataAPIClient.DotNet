using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using log4net;
using Newtonsoft.Json.Linq;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk
{
    public class ResourceSingleQueue : Endpoint, IResource, IDisposable, IStreamStatistics
    {
        private bool _isStreamStopped;

        private IObserver<string> _observer;
        private readonly AmqpSubscriber _amqpSubscriber;


        internal ResourceSingleQueue(RestItem restItem, IConnectClient connectClient, AmqpSubscriber amqpSubscriber)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(ResourceSingleQueue).ToString());
            _amqpSubscriber = amqpSubscriber;
        }

        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
        public event EventHandler StreamSynchronizationError;

        public string Id { get { return State.Content.Id; } }
        public string Name { get { return State.Name; } }
        public int LastSequence { get; set; }
        public DateTime LastMessageReceived { get; private set; }
        public DateTime LastStreamDisconnect { get; private set; }
        public double EchoRoundTripInMilliseconds { get; private set; }
        public bool IsStreamActive { get; set; }

        public Summary Content
        {
            get { return State.Content; }
        }

        public string GetSnapshot()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get Snapshot for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            
            var snapshot = FindRelationAndFollowAsString("http://api.sportingsolutions.com/rels/snapshot","GetSnapshot Http Error",loggingStringBuilder);
            Logger.Info(loggingStringBuilder);
            return snapshot;
        }

        public void StartStreaming()
        {
            IsStreamActive = true;

            _observer = Observer.Create<string>(update =>
                {
                    Logger.DebugFormat("Stream update arrived to a resource with fixtureId={0}", this.Id);

                    LastMessageReceived = DateTime.Now;

                    var updateSequence = GetSequenceFromStreamUpdate(update);

                    if (updateSequence > LastSequence)
                    {
                        LastSequence = updateSequence; 
                    }

                    Task.Factory.StartNew(
                        () =>
                            {
                                if (StreamEvent != null)
                                {
                                    StreamEvent(this, new StreamEventArgs(update));
                                }
                            });
                });

            _amqpSubscriber.StartStream(this, _observer);
        }

        public static int GetSequenceFromStreamUpdate(string update)
        {
            var jobject = JObject.Parse(update);

            return jobject["Content"]["Sequence"].Value<int>();
        }

        internal void PushValueToObserver(string value)
        {
            if (_observer != null && !string.IsNullOrWhiteSpace(value))
            {
                _observer.OnNext(value);
            }
        }

        internal void FireStreamConnected()
        {
            if (StreamConnected != null)
            {
                StreamConnected(this, EventArgs.Empty);
            }
        }

        internal void FireStreamDisconnected()
        {
            LastStreamDisconnect = DateTime.Now;

            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, EventArgs.Empty);
            }
        }

        public IObservable<string> GetStreamData()
        {
            return null;
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
            Logger.InfoFormat("Stopping streaming for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            if (!_isStreamStopped)
            {
                _isStreamStopped = true;

                _amqpSubscriber.StopStream(this.Id);

                if (StreamDisconnected != null)
                {
                    StreamDisconnected(this, EventArgs.Empty);
                }
            }
        }

        internal QueueDetails GetQueueDetails()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get AMQPStream for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp","GetAMQPStream Http Error", loggingStringBuilder);
            Logger.Info(loggingStringBuilder);

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

        public void Dispose()
        {
            this.StopStreaming();
        }
    }
}
