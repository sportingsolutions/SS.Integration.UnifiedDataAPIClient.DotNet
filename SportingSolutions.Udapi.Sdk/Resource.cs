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
using System.Reactive;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource, IDisposable, IStreamStatistics
    {
        private bool _isStreaming;
        private readonly ManualResetEvent _pauseStream;
        private bool _isStreamStopped;
        private IModel _channel;
        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
        public event EventHandler StreamSynchronizationError;
        private IObserver<string> _streamObserver;
        public readonly EchoController EchoController;

        internal Resource(RestItem restItem, IConnectClient connectClient, StreamController streamController, Uri echoUri)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Resource).ToString());
            Logger.DebugFormat("Instantiated fixtureName=\"{0}\"", restItem.Name);
            _pauseStream = new ManualResetEvent(true);
            EchoController = new EchoController(ConnectClient, echoUri);
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
            _streamObserver = Observer.Create<string>(ProcessMessage);
            StreamSubscriber.SubscribeStream(this, _streamObserver);
        }

        private void ProcessMessage(string message)
        {
            Logger.DebugFormat("Stream message arrived to a resource with fixtureId={0}", this.Id);

            if (StreamEvent != null)
            {
                StreamEvent(this, new StreamEventArgs(message));
            }
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
            Logger.InfoFormat("Stopping streaming for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            if (!_isStreamStopped)
            {
                _isStreaming = false;
                _isStreamStopped = true;

                StreamSubscriber.StopStream(this.Id);

                if (StreamDisconnected != null)
                {
                    StreamDisconnected(this, EventArgs.Empty);
                }
            }
            //_isStreaming = false;
            //_cancellationTokenSource.Cancel();
            //if (_consumer != null)
            //{
            //    try
            //    {
            //        _channel.BasicCancel(_consumer.ConsumerTag);
            //    }
            //    catch (Exception ex)
            //    {
            //        Logger.Error(string.Format("Problem when stopping stream for fixtureId={0} fixtureName=\"{1}\"", Id, Name), ex);
            //        Dispose();
            //    }
            //}
            //else
            //{
            //    Dispose();
            //}
        }

        public void Dispose()
        {
            Logger.InfoFormat("Streaming stopped for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            if (_channel != null)
            {
                try
                {
                    _channel.Dispose();
                    _channel.Close();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
                _channel = null;
            }

            Logger.InfoFormat("Streaming Channel Closed for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            if (StreamDisconnected != null)
            {
                StreamDisconnected(this, new EventArgs());
            }
        }

        public QueueDetails GetQueueDetails()
        {
            var loggingStringBuilder = new StringBuilder();
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp", "GetAmqpStream Http Error", loggingStringBuilder);
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

        public void StartEchos(string virtualHost, int echoInterval)
        {
            EchoController.StartEchos(virtualHost, echoInterval);
        }

        public void StopEcho()
        {
            EchoController.StopEchos();
        }
    }

    public class EchoController
    {
        private readonly ILog _logger;
        private static readonly object InitSync = new Object();
        private Timer _echoTimer;
        private Guid _lastSentGuid;
        private readonly IConnectClient _connectClient;
        private readonly Uri _echoUri;

        public EchoController(IConnectClient connectClient, Uri echoUri)
        {
            _logger = LogManager.GetLogger(typeof(EchoController));          
            _connectClient = connectClient;
            _echoUri = echoUri;
        }

        public void StartEchos(string virtualHost, int echoInterval)
        {
            lock (InitSync)
            {
                if (_echoTimer == null)
                {
                    _echoTimer = new Timer(x => SendEcho(_echoUri, virtualHost), null, 0, echoInterval);
                }
            }
        }

        public void StopEchos()
        {
            _echoTimer.Dispose();
            _echoTimer = null;
        }

        private void SendEcho(Uri echoUri, string virtualHost)
        {
            try
            {
                _lastSentGuid = Guid.NewGuid();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var streamEcho = new StreamEcho
                {
                    Host = virtualHost,
                    Message = _lastSentGuid.ToString() + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                var response = _connectClient.Request(echoUri, Method.POST, streamEcho, "application/json", 3000);

                if (response == null)
                {
                    _logger.WarnFormat("Post Echo had null response and took duration={0}ms", stopwatch.ElapsedMilliseconds);

                }
                else if (response.ErrorException != null)
                {
                    RestErrorHelper.LogRestError(_logger, response, string.Format("Echo Http Error took {0}ms", stopwatch.ElapsedMilliseconds));
                }
                else
                {
                    _logger.DebugFormat("Post Echo took duration={0}ms", stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Unable to post echo", ex);
            }
        }
    }
}
