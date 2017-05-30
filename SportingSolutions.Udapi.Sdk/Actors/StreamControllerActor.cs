using System;
using System.Threading;
using Akka.Actor;
using log4net;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    /// <summary>
    /// 
    ///     The StreamController is responsible for managing the connection
    ///     to the RabbitMQ streaming server.
    /// 
    ///     There is only ONE streaming connection, independently of how
    ///     many resources/consumers are added.
    /// 
    ///     Each consumer has its own queue, but the connection is shared
    ///     among all the consumers. If the connection goes down, all
    ///     the consumers get disconnected. 
    /// 
    ///     There is no automatic re-connection. A connection is (re)-established
    ///     when the first consumer is added.
    /// 
    ///     Once a connection is established the StreamSubscriber object
    ///     is set to read from the connection for any up coming messages.
    /// 
    ///     The StreamSubscriber then passed this object to the IDispatch
    ///     object whose task it to dispatch the messages to the correct
    ///     consumer.
    /// 
    /// </summary>
    internal class StreamControllerActor : ReceiveActor, IWithUnboundedStash
    {
        public const string ActorName = "StreamControllerActor";
        public const string QueueName = "TestQueue";

        internal enum ConnectionState
        {
            DISCONNECTED = 0,
            CONNECTING = 1,
            CONNECTED = 2,
            STREAMING = 3
        }

        private static readonly ILog _logger = LogManager.GetLogger(typeof(StreamControllerActor));

        protected IConnection _streamConnection;
        private volatile ConnectionState _state;
        private readonly ICancelable _connectionCancellation = new Cancelable(Context.System.Scheduler);
        private StreamSubscriber _subscriber;

        public StreamControllerActor(IActorRef dispatcherActorRefActor)
        {
            if (dispatcherActorRefActor == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcherActorRefActor;

            //Start in Disconnected state
            DisconnectedState();

            AutoReconnect = UDAPI.Configuration.AutoReconnect;

            _logger.DebugFormat("StreamController initialised");
        }

        /// <summary>
        /// This is the state when the connection is closed
        /// </summary>
        private void DisconnectedState()
        {
            Receive<ValidateMessage>(x => ValidateConnection(x));
            Receive<ConnectedMessage>(x => Become(ConnectedState));
            Receive<NewConsumerMessage>(x =>
            {
                Stash.Stash();
                Connect(x.Consumer);

            });
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());

            State = ConnectionState.DISCONNECTED;

            _logger.Info("Moved to DisconnectedState");
        }

        /// <summary>
        /// this is the state when the connection is being automatically recovered by RabbitMQ
        /// </summary>
        private void ValidationState()
        {
            Receive<DisconnectedMessage>(x => Become(DisconnectedState));
            Receive<ValidateMessage>(x => ValidateConnection(x));
            Receive<ValidationSucceededMessage>(x => ValidationSucceededHandler(x));
            Receive<NewConsumerMessage>(x => Stash.Stash());
            Receive<RemoveConsumerMessage>(x => Stash.Stash());
            Receive<DisposeMessage>(x => Dispose());
            Receive<AllConsumersDisconnectedMessage>(x => Stash.Stash());

            State = ConnectionState.DISCONNECTED;
            _logger.Info("Moved to ValidationState");
        }

        private void ValidationSucceededHandler(ValidationSucceededMessage message)
        {
            switch (message.RestoredConnectionState)
            {
                case ConnectionState.CONNECTED:
                {
                    Become(ConnectedState);
                    break;
                }
                case ConnectionState.STREAMING:
                {
                    Become(StreamingState);
                    break;
                }
            }
        }

        /// <summary>
        /// this is the state when the connection is open
        /// </summary>
        private void ConnectedState()
        {
            Receive<ValidationStartMessage>(x => ValidationStart(ConnectionState.CONNECTED));
            Receive<NewConsumerMessage>(x => ProcessNewConsumer(x));
            Receive<DisconnectedMessage>(x => Become(DisconnectedState));
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateMessage>(x => ValidateConnection(x));
            Receive<AllConsumersDisconnectedMessage>(x => CloseConnection());
            Stash.UnstashAll();

            State = ConnectionState.CONNECTED;
            _logger.Info("Moved to Connected State");
        }

        private void StreamingState()
        {
            Receive<ValidationStartMessage>(x => ValidationStart(ConnectionState.STREAMING));
            Receive<NewConsumerMessage>(x => Dispatcher.Tell(x));
            Receive<DisconnectedMessage>(x => Become(DisconnectedState));
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateMessage>(x => ValidateConnection(x));
            Receive<AllConsumersDisconnectedMessage>(x => CloseConnection());

            State = ConnectionState.STREAMING;
            _logger.Info("Moved to Streaming State");
        }

        private void ValidationStart(ConnectionState connectionState)
        {
            Become(ValidationState);

            //schedule check in the near future (10s by default) whether the connection has recovered
            //DO NOT use Context in here as this code is likely going to be called as a result of event being raised on a separate thread 
            //Calling Context.Scheduler will result in exception as it's not run within Actor context - this code communicates with the actor via ActorSystem instead
            SdkActorSystem.ActorSystem.Scheduler.ScheduleTellOnce(
                TimeSpan.FromSeconds(UDAPI.Configuration.DisconnectionDelay)
                , SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                , new ValidateMessage {RestoredConnectionState = connectionState}
                , ActorRefs.NoSender);
        }

        private void ProcessNewConsumer(NewConsumerMessage newConsumerMessage)
        {
            AddConsumerToQueue(newConsumerMessage.Consumer);
            Dispatcher.Tell(new NewConsumerMessage {Consumer = newConsumerMessage.Consumer});
            Become(StreamingState);
        }

        /// <summary>
        /// Is AutoReconnect enabled
        /// </summary>
        public bool AutoReconnect { get; private set; }



        /// <summary>
        /// 
        ///     Returns the IDispatcher object that is responsible
        ///     of dispatching messages to the consumers.
        /// 
        /// </summary>
        internal IActorRef Dispatcher { get; private set; }

        public IStash Stash { get; set; }

        internal Exception ConnectionError;

        internal ConnectionState State
        {
            get
            {
                // as it is volatile, reading this property is thread-safe
                return _state;
            }
            private set { _state = value; }
        }

        #region Connection management

        protected virtual void CloseConnection()
        {
            try
            {
                if (_streamConnection != null)
                {
                    _streamConnection.ConnectionShutdown -= OnConnectionShutdown;
                    if (_streamConnection.IsOpen)
                        _streamConnection.Close();
                    _streamConnection.Dispose();
                }
            }
            catch (Exception)
            {
            }

            try
            {
                if (_subscriber != null)
                {
                    _subscriber.StopConsuming();
                    _subscriber.Dispose();
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                Dispatcher.Tell(new RemoveAllConsumersMessage());
                _subscriber = null;
                _streamConnection = null;
            }

            OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
        }

        protected virtual void EstablishConnection(ConnectionFactory factory)
        {
            // this method doesn't quit until
            // 1) a connection is established
            // 2) Dispose() is called
            //
            // therefore we will NOT quit from this method
            // when the StreamController has State = CONNECTING
            //
            // it must be called in mutual exclusion:
            // _connectionLock must be acquire before calling
            // this method.

            var newstate = ConnectionState.DISCONNECTED;

            try
            {
                _logger.DebugFormat("Connecting to the streaming server");

                long attempt = 1;
                bool result = false;
                while (!_connectionCancellation.IsCancellationRequested && !result)
                {
                    _logger.DebugFormat("Establishing connection to the streaming server, attempt={0}", attempt);

                    try
                    {
                        _streamConnection = factory.CreateConnection();
                        _streamConnection.ConnectionShutdown += OnConnectionShutdown;

                        _logger.Info("Connection to the streaming server correctly established");

                        newstate = ConnectionState.CONNECTED;
                        result = true;

                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error connecting to the streaming server", ex);
                        Thread.Sleep(100);
                    }

                    attempt++;
                }
            }
            finally
            {
                // notify any sleeping threads
                OnConnectionStatusChanged(newstate);
            }
        }

        private void Connect(IConsumer consumer)
        {
            if (State == ConnectionState.CONNECTED) return;

            if (State == ConnectionState.CONNECTED || _connectionCancellation.IsCancellationRequested)
                return;

            State = ConnectionState.CONNECTING;

            // GetQueueDetails() returns the credentials for connecting to the AMQP server
            // but it also asks the server to create an AMQP queue for the caller.
            // As the time to establish a connection could take a while (just think about
            // a not reliable network), the created AMQP queue could expire before we 
            // call BasicConsume() on it. 
            //
            // To prevent this situation, we call GetQueueDetails() twice for the consumer
            // who establish the connection, one here, and the second time on  
            // AddConsumeToQueue()

            QueueDetails queue = null;
            try
            {
                queue = consumer.GetQueueDetails();
                if (queue == null || string.IsNullOrEmpty(queue.Name))
                {
                    var e = new Exception("queue's name is not valid for consumerId=" + consumer.Id);
                    ConnectionError = e;
                    throw e;
                }
            }
            catch (Exception e)
            {
                _logger.Error("Error acquiring queue details for consumerId=" + consumer.Id, e);
                OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
                ConnectionError = e;
                throw;
            }

            var factory = new ConnectionFactory
            {
                RequestedHeartbeat = UDAPI.Configuration.AMQPMissedHeartbeat,
                HostName = queue.Host,
                AutomaticRecoveryEnabled = UDAPI.Configuration.AutoReconnect,
                Port = queue.Port,
                UserName = queue.UserName,
                Password = queue.Password,
                VirtualHost = "/" + queue.VirtualHost // this is not used anymore, we keep it for retro-compatibility
            };

            EstablishConnection(factory);
        }

        internal virtual void OnConnectionShutdown(object sender, ShutdownEventArgs sea)
        {
            _logger.ErrorFormat(
                "The AMQP connection was shutdown: {0}. AutoReconnect is enabled={1}", sea,
                AutoReconnect);

            if (!AutoReconnect)
            {
                CloseConnection();
            }
            else
            {
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                    .Tell(new ValidationStartMessage());
            }
        }

        private void ValidateConnection(ValidateMessage validateMessage)
        {
            //validate whether the reconnection was successful 
            _logger.InfoFormat("Starting validation for reconnection connHash={0}",
                _streamConnection?.GetHashCode().ToString() ?? "null");

            //in case the connection is swapped by RMQ library while the check is running
            var testConnection = _streamConnection;

            if (testConnection == null)
            {
                _logger.WarnFormat(
                    "Reconnection failed, connection has been disposed, the disconnection event needs to be raised");
                CloseConnection();
                return;
            }

            _logger.InfoFormat("Veryfing that connection is open ? {0}", testConnection.IsOpen);

            if (testConnection.IsOpen)
            {
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new ResetEchoesMessage());
                _logger.InfoFormat("Reconnection successful, disconnection event will not be raised");

                Self.Tell(new ValidationSucceededMessage
                {
                    RestoredConnectionState = validateMessage.RestoredConnectionState
                });
            }
            else
            {
                _logger.Warn(
                    "Connection validation failed, connection is not open - calling CloseConnection() to dispose it");
                CloseConnection();
            }
        }

        protected virtual void OnConnectionStatusChanged(ConnectionState newState)
        {
            State = newState;
            object message = null;

            switch (newState)
            {
                case ConnectionState.DISCONNECTED:
                    message = new DisconnectedMessage();
                    break;
                case ConnectionState.CONNECTED:
                    message = new ConnectedMessage();
                    ConnectionError = null;
                    break;
                case ConnectionState.CONNECTING:
                    break;
            }

            if (message != null)
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(message);
        }

        #endregion

        #region Consumer

        protected virtual void AddConsumerToQueue(IConsumer consumer)
        {
            var queue = consumer.GetQueueDetails();
            if (string.IsNullOrEmpty(queue?.Name))
                throw new Exception("Invalid queue details");

            var model = _streamConnection.CreateModel();
            StreamSubscriber subscriber = null;

            try
            {
                _subscriber = new StreamSubscriber(model, consumer, Dispatcher);
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new NewConsumerMessage {Consumer = consumer});
                _subscriber.StartConsuming(QueueName);
            }
            catch
            {
                _subscriber?.Dispose();
                _subscriber = null;

                throw;
            }
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentNullException("consumer");

            RemoveConsumerFromQueue(consumer);
        }

        protected virtual void RemoveConsumerFromQueue(IConsumer consumer)
        {
            var disconnectMsg = new DisconnectMessage {Id = consumer.Id};
            Dispatcher.Tell(disconnectMsg);
        }

        #endregion

        #region IDisposable Members

        public virtual void Shutdown()
        {
            _logger.Debug("Shutting down StreamController");
            _connectionCancellation.Cancel();

            Dispatcher.Tell(new DisposeMessage());
            CloseConnection();

            _logger.Info("StreamController correctly disposed");
        }

        public void Dispose()
        {
            Shutdown();
        }

        #endregion

        #region Private messages

        private class ConnectedMessage
        {

        }

        private class DisconnectedMessage
        {

        }

        private class ValidateMessage
        {
            internal ConnectionState RestoredConnectionState { get; set; }
        }

        private class ValidationStartMessage
        {
        }

        private class ValidationSucceededMessage
        {
            internal ConnectionState RestoredConnectionState { get; set; }
        }

        #endregion
    }
}
