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
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Resource : Endpoint, IResource, IDisposable, IStreamStatistics
    {
        private bool _isStreaming;
        private readonly ManualResetEvent _pauseStream;

        private IModel _channel;
        private QueueingCustomConsumer _consumer;
        private string _virtualHost;
        private string _queueName;

        private int _echoSenderInterval;

        private int _reconnectionsSinceLastMessage;
        private int _maxRetries;

        private bool _isShutdown;

        private CancellationTokenSource _cancellationTokenSource;

        private readonly StreamController _streamController;
        private Task _streamTask;


        public event EventHandler StreamConnected;
        public event EventHandler StreamDisconnected;
        public event EventHandler<StreamEventArgs> StreamEvent;
        public event EventHandler StreamSynchronizationError;

        internal Resource(RestItem restItem, IConnectClient connectClient, StreamController streamController)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Resource).ToString());
            Logger.DebugFormat("Instantiated fixtureName=\"{0}\"", restItem.Name);
            _streamController = streamController;
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
            if (State == null)
            {
                throw new NullReferenceException("Can't start streaming! State was null on Resource (no details are available due to lack of state)");
            }

            if (_streamTask != null && !_streamTask.IsCompleted)
                throw new Exception(string.Format("Requested start streaming while already streaming {0}", this));

            Logger.InfoFormat("Starting stream for fixtureName=\"{0}\" fixtureId={1} with Echo Interval of {2}", Name, Id, echoInterval);

            _echoSenderInterval = echoInterval;
            
            _isShutdown = false;
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            _streamTask = Task.Factory.StartNew(() => StreamData(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _streamTask.ContinueWith(t => Logger.InfoFormat("Stream cancelled for fixtureName=\"{0}\" fixtureId={1}", Name, Id), TaskContinuationOptions.OnlyOnCanceled);

        }

        private void StreamData(CancellationToken cancellationToken)
        {
            var missedEchos = 0;

            _maxRetries = 5;
            _isStreaming = true;

            Reconnect();

            Logger.InfoFormat("Initialised connection to Streaming Queue for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

            LastMessageReceived = DateTime.UtcNow;

            _consumer.QueueCancelled += Dispose;

            while (_isStreaming && !cancellationToken.IsCancellationRequested && !_isShutdown)
            {
                try
                {
                    _pauseStream.WaitOne();
                    BasicDeliverEventArgs output = null;

                    if (_channel == null || !_channel.IsOpen)
                    {
                        throw new Exception("Channel is closed");
                    }

                    if (StreamConnected != null)
                    {
                        HandleExceptionAndLog(() => StreamConnected(this, new EventArgs()), "Error occured when executing StreamConnected event");
                    }

                    if (_consumer.Queue.Dequeue(_echoSenderInterval + 3000, out output))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        var deliveryArgs = output;
                        var message = deliveryArgs.Body;

                        LastMessageReceived = DateTime.UtcNow;
                        missedEchos = 0;
                        _reconnectionsSinceLastMessage = 0;

                        var messageString = Encoding.UTF8.GetString(message);
                        var jobject = JObject.Parse(messageString);
                        if (jobject["Relation"].Value<string>() == "http://api.sportingsolutions.com/rels/stream/echo")
                        {
                            ProcessEcho(jobject);
                        }
                        else
                        {
                            Logger.DebugFormat("Update arrived for fixtureId={0} fixtureName={1}", Id, Name);
                            if (StreamEvent != null)
                            {
                                HandleExceptionAndLog(() => StreamEvent(this, new StreamEventArgs(messageString)), "Error occured when processing StreamEvent event");
                            }
                            else
                            {
                                Logger.DebugFormat("No event handler attached to StreamEvent for fixtureId={0} fixtureName={1}", Id, Name);
                            }
                        }
                    }
                    else
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        // message didn't arrive in specified time period

                        missedEchos++;
                        Logger.InfoFormat("No echo recieved for fixtureId={0} fixtureName=\"{1}\" missedEchos={2}", Id, Name, missedEchos);

                        if (missedEchos > 3)
                        {
                            Logger.WarnFormat("Missed 3 echos disconnecting stream for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
                            LastStreamDisconnect = DateTime.UtcNow;
                            //reached timeout, no echo has arrived
                            StopStreaming();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested || _isStreaming)
                    {
                        Logger.Error(string.Format("Lost connection to stream for fixtureName=\"{0}\" fixtureId={1}", Name, Id), ex);
                        LastStreamDisconnect = DateTime.UtcNow;

                        _isStreaming = false;
                    }
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void ProcessEcho(JObject jobject)
        {
            var split = jobject["Content"].Value<String>().Split(';');
            var timeSent = DateTime.ParseExact(split[1], "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var roundTripTime = DateTime.Now - timeSent;

            var roundMillis = roundTripTime.TotalMilliseconds;

            EchoRoundTripInMilliseconds = roundMillis;

            Logger.DebugFormat("Echo recieved for fixtureId={0} fixtureName=\"{1}\"", Id, Name);
        }

        private void Reconnect()
        {
            Logger.WarnFormat("Attempting to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}", Name, Id, _reconnectionsSinceLastMessage + 1);
            var success = false;
            while (!success && _isStreaming && !_isShutdown)
            {
                try
                {
                    if (_reconnectionsSinceLastMessage > _maxRetries)
                    {
                        Logger.ErrorFormat("Failed to reconnect Stream for fixtureName=\"{0}\" fixtureId={1} ", Name,
                                            Id);
                        StopStreaming();
                        return;
                    }

                    if (_isShutdown)
                    {
                        Logger.InfoFormat("Stream is shutting down for: {0}, reconnect will be cancelled. ", this);
                        return;
                    }

                    var loggingStringBuilder = new StringBuilder();
                    var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/stream/amqp", "GetAmqpStream Http Error", loggingStringBuilder);
                    Logger.Info(loggingStringBuilder);
                    var amqpLink = restItems.SelectMany(restItem => restItem.Links).First(restLink => restLink.Relation == "amqp");

                    var amqpUri = new Uri(amqpLink.Href);

                    var host = amqpUri.Host;

                    var port = amqpUri.Port;

                    var userInfo = amqpUri.UserInfo;
                    userInfo = HttpUtility.UrlDecode(userInfo);
                    var user = string.Empty;
                    var password = string.Empty;
                    if (!String.IsNullOrEmpty(userInfo))
                    {
                        var userPass = userInfo.Split(':');
                        if (userPass.Length > 2)
                        {
                            throw new ArgumentException(string.Format("Bad user info in AMQP URI: {0}", userInfo));
                        }
                        user = userPass[0];

                        if (userPass.Length == 2)
                        {
                            password = userPass[1];
                        }
                    }
                    _queueName = "";
                    var path = amqpUri.AbsolutePath;
                    if (!String.IsNullOrEmpty(path))
                    {
                        _queueName = path.Substring(path.IndexOf('/', 1) + 1);
                        _virtualHost = path.Substring(1, path.IndexOf('/', 1) - 1);
                    }

                    if (_channel != null)
                    {
                        _channel.Close();
                        _channel = null;
                    }

                    _channel = _streamController.GetStreamChannel(host, port, user, password, _virtualHost);

                    _consumer = new QueueingCustomConsumer(_channel);
                    _channel.BasicConsume(_queueName, true, _consumer);
                    _channel.BasicQos(0, 10, false);
                    _channel.ModelShutdown += ChannelModelShutdown;
                    success = true;
                    _isShutdown = false;
                    _streamController.StartEcho(_virtualHost, _echoSenderInterval);
                }
                catch (Exception ex)
                {
                    // give time for load balancer to notice the node is down
                    Thread.Sleep(500);

                    Logger.Warn(string.Format("Failed to reconnect stream for fixtureName=\"{0}\" fixtureId={1}, Attempt {2}", Name, Id,
                        _reconnectionsSinceLastMessage + 1), ex);
                }
                finally
                {
                    _reconnectionsSinceLastMessage++;
                }
            }
        }

        private void ChannelModelShutdown(IModel model, ShutdownEventArgs reason)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("Channel is shutdown for fixtureName=\"{0}\" fixtureId={1}", Name, Id).AppendLine();
            stringBuilder.Append(reason);

            Logger.Info(stringBuilder.ToString());
            Dispose();
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
            _isStreaming = false;

            Logger.DebugFormat("Streaming stop requested for fixtureId={0}", Id);

            _cancellationTokenSource.Cancel();
            if (_consumer != null)
            {
                try
                {
                    _channel.BasicCancel(_consumer.ConsumerTag);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Problem when stopping stream for fixtureId={0} fixtureName=\"{1}\"", Id, Name), ex);
                    Dispose();
                }
            }
            else
            {
                Dispose();
            }
        }

        private void HandleExceptionAndLog(Action expressionToExecute, string message = null)
        {
            try
            {
                expressionToExecute();
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("{0} {1} {2}", this, message, ex));
            }
        }

        public void Dispose()
        {
            _isStreaming = false;

            if (!_isShutdown)
            {
                _isShutdown = true;
                Task.Factory.StartNew(() =>
                {
                    Logger.InfoFormat("Streaming stopped for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
                    if (_channel != null)
                    {
                        try
                        {
                            if (_channel.IsOpen)
                                _channel.Close();
                            _channel.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex);
                        }
                        _channel = null;
                    }

                    Logger.InfoFormat("Streaming Channel Closed for fixtureName=\"{0}\" fixtureId={1}", Name, Id);

                }).ContinueWith(x =>
                {
                    if (StreamDisconnected != null)
                    {
                        HandleExceptionAndLog(() => StreamDisconnected(this, new EventArgs()), "Error occured when processing StreamDisconnected event");
                    }
                });
            }


        }

        public override string ToString()
        {
            return string.Format("Resource fixtureId={0} fixtureName=\"{1}\"", Id, Name);
        }
    }
}
