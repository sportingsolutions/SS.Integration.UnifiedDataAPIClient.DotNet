using System;
using System.Threading;
using System.Threading.Tasks;
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


        internal enum ConnectionState
        {
            DISCONNECTED = 0,
            CONNECTING = 1,
            CONNECTED = 2
        }


        public const string ActorName = "StreamControllerActor";
        public int timeoutCounter = 0;
        public const int TimeoutCounterLimit = 20;
        private static ICancelable _validateCancellation;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(StreamControllerActor));
        protected IConnection _streamConnection;
        private volatile ConnectionState _state;
        private readonly ICancelable _connectionCancellation = new Cancelable(Context.System.Scheduler);


        public StreamControllerActor(IActorRef dispatcherActor)
        {
            if (dispatcherActor == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcherActor;
            timeoutCounter = 0;
            //Start in Disconnected state
            DisconnectedState();

            AutoReconnect = UDAPI.Configuration.AutoReconnect;
            CancelValidationMessages();
            _validateCancellation = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(60000, 60000, Self, new ValidateStateMessage(), Self);

            _logger.DebugFormat("StreamController initialised, AutoReconnect={0}", AutoReconnect);
        }

        private static void CancelValidationMessages()
        {

            if (_validateCancellation == null)
                return;
            _logger.Debug("CancelValidationMessages triggered");
            _validateCancellation.Cancel();
            _validateCancellation = null;
        }

        protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));

            CancelValidationMessages();

            base.PreRestart(reason, new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
        }

        /// <summary>
        /// This is the state when the connection is closed
        /// </summary>
        private void DisconnectedState()
        {
            _logger.Info("Moved to DisconnectedState");

            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ConnectedMessage>(x => Become(ConnectedState));

            Receive<NewConsumerMessage>(x =>
            {
                Stash.Stash();
                GetQueueDetailsAndEstablisConnection(x.Consumer);

            });
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateStateMessage>(x => ValidateState(x));
            Receive<DisconnectedMessage>(x => DisconnecteOnDisconnectedHandler(x));

            State = ConnectionState.DISCONNECTED;
        }




        /// <summary>
        /// this is the state when the connection is being automatically recovered by RabbitMQ
        /// </summary>
        private void ValidationState()
        {
            _logger.Info("Moved to ValidationState");

            Receive<DisconnectedMessage>(x => DisconnectedHandler(x));
            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ValidationSucceededMessage>(x => Become(ConnectedState));
            Receive<NewConsumerMessage>(x => Stash.Stash());
            Receive<RemoveConsumerMessage>(x => Stash.Stash());
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateStateMessage>(x => ValidateState(x));

            State = ConnectionState.DISCONNECTED;
        }



        /// <summary>
        /// this is the state when the connection is open
        /// </summary>
        private void ConnectedState()
        {
            _logger.Info("Moved to ConnectedState");

            Receive<ValidationStartMessage>(x => ValidationStart(x));
            Receive<NewConsumerMessage>(x => ProcessNewConsumer(x));
            Receive<DisconnectedMessage>(x => DisconnectedHandler(x));
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ValidateStateMessage>(x => ValidateState(x));

            Stash.UnstashAll();
            State = ConnectionState.CONNECTED;
        }

        private void ValidationStart(ValidationStartMessage validationStartMessage)
        {
            _logger.Debug("ValidationStart triggered");
            Become(ValidationState);

            //schedule check in the near future (10s by default) whether the connection has recovered
            //DO NOT use Context in here as this code is likely going to be called as a result of event being raised on a separate thread 
            //Calling Context.Scheduler will result in exception as it's not run within Actor context - this code communicates with the actor via ActorSystem instead
            SdkActorSystem.ActorSystem.Scheduler.ScheduleTellOnce(
                TimeSpan.FromSeconds(UDAPI.Configuration.DisconnectionDelay)
                , SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                , new ValidateConnectionMessage()
                , ActorRefs.NoSender);
        }

        private void ProcessNewConsumer(NewConsumerMessage newConsumerMessage)
        {
            var consumer = newConsumerMessage.Consumer;
            if (consumer == null)
            {
                _logger.Warn("Method=ProcessNewConsumer Consumer is null");
                return;
            }
            _logger.Debug($"Method=ProcessNewConsumer triggered consumr={consumer.Id}");

            var queue = consumer.GetQueueDetails();

            if (string.IsNullOrEmpty(queue?.Name))
            {
                _logger.Warn("Method=ProcessNewConsumer Invalid queue details");
                return;
            }

            if (_streamConnection == null)
            {
                _logger.Warn($"Method=ProcessNewConsumer StreamConnection is null currentState={State.ToString()}");
                if (UDAPI.Configuration.UseStreamControllerMailbox)
                    Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
                else
                    DisconnectedHandler(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
                Self.Tell(newConsumerMessage);
                return;
            }

            if (!_streamConnection.IsOpen)
            {
                _logger.Warn($"Method=ProcessNewConsumer StreamConnection is closed currentState={State.ToString()}");
                if (UDAPI.Configuration.UseStreamControllerMailbox)
                    Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
                else
                    DisconnectedHandler(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
                Self.Tell(newConsumerMessage);
                return;
            }

            var model = _streamConnection.CreateModel();

            StreamSubscriber subscriber = null;

            try
            {
                subscriber = new StreamSubscriber(model, consumer, Dispatcher);
                subscriber.StartConsuming(queue.Name);
            }
            catch (Exception e)
            {
                if (subscriber != null)
                    subscriber.Dispose();
                timeoutCounter++;
                _logger.Warn($"Method=ProcessNewConsumer StartConsuming errored for consumerId={consumer.Id} {e}");
                if (timeoutCounter > TimeoutCounterLimit)
                    throw;
            }
            _logger.Debug($"Method=ProcessNewConsumer successfully executed consumr={consumer.Id}");
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
        private ConnectionFactory _connectionFactory;

        internal ConnectionState State
        {
            get => _state;
            private set => _state = value;
        }

        #region Connection management

        protected virtual void CloseConnection()
        {
            if (_streamConnection != null)
            {
                _logger.Debug($"CloseConnection triggered {_streamConnection}");
                try
                {
                    {
                        _streamConnection.ConnectionShutdown -= OnConnectionShutdown;
                        if (_streamConnection.IsOpen)
                        {
                            _streamConnection.Close();
                            _logger.Debug("Connection Closed");
                        }
                        
                    }
                }
                catch (Exception e)
                {
                    _logger.Warn($"Failed to close connection {e}");
                }

                try
                {
                    {
                        _streamConnection.Dispose();
                        _logger.Debug("Connection Disposed");
                    }
                }
                catch (Exception e)
                {
                    _logger.Warn($"Failed to dispose connection {e}");
                }
                _streamConnection = null;

            }
            else
            {
                _logger.Debug("No need to CloseConnection");
            }



        }

        protected void NotifyDispatcherConnectionError()
        {
            try
            {
                Dispatcher.Tell(new RemoveAllSubscribers());
            }
            catch (Exception e)
            {
                _logger.Warn($"Failed to tell diapstcher RemoveAllSubscribers diapstcher={Dispatcher}");
            }
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

            _logger.DebugFormat("Connecting to the streaming server");

            if (factory == null)
            {
                _logger.Warn("Connecting to the streaming server Failed as connectionFactory=NULL");
                return;
            }

            State = ConnectionState.CONNECTING;

            long attempt = 1;
            while (!_connectionCancellation.IsCancellationRequested)
            {
                _logger.DebugFormat("Establishing connection to the streaming server, attempt={0}", attempt);

                try
                {
                    _streamConnection = factory.CreateConnection();
                    _streamConnection.ConnectionShutdown += OnConnectionShutdown;

                    _logger.Info("Connection to the streaming server correctly established");

                    Self.Tell(new ConnectedMessage());
                    return;

                }
                catch (Exception ex)
                {
                    _logger.Error("Error connecting to the streaming server", ex);
                    Thread.Sleep(100);
                }

                attempt++;
            }

            State = ConnectionState.DISCONNECTED;


        }

        

        private void GetQueueDetailsAndEstablisConnection(IConsumer consumer)
        {
            _logger.Debug($"GetQueueDetailsAndEstablisConnection triggered state={State} isCancellationRequested={_connectionCancellation.IsCancellationRequested}");


            if (State == ConnectionState.CONNECTED || State == ConnectionState.CONNECTING ||
                _connectionCancellation.IsCancellationRequested)
            {
                _logger.Info($"GetQueueDetailsAndEstablisConnection will not be executed state={State} isCancellationRequested={_connectionCancellation.IsCancellationRequested}");
                return;
            }
            
            CreateConectionFactory(consumer);
            EstablishConnection(_connectionFactory);
        }

        private void CreateConectionFactory(IConsumer consumer)
        {
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
                ConnectionError = e;
                throw;
            }

            _connectionFactory = new ConnectionFactory
            {
                RequestedHeartbeat = UDAPI.Configuration.AMQPMissedHeartbeat,
                HostName = queue.Host,
                AutomaticRecoveryEnabled = AutoReconnect,
                Port = queue.Port,
                UserName = queue.UserName,
                Password = queue.Password,
                VirtualHost = "/" + queue.VirtualHost // this is not used anymore, we keep it for retro-compatibility
            };
        }

        internal virtual void OnConnectionShutdown(object sender, ShutdownEventArgs sea)
        {
            _logger.Error($"The AMQP connection was shutdown. AutoReconnect is enabled={AutoReconnect}, sender={sender} {sea}");

            
            if (!AutoReconnect)
            {
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
            }
            else
            {
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new ValidationStartMessage());
            }
        }

        private void ValidateState(ValidateStateMessage validateStateMessage)
        {
            _logger.Warn($"Method=ValidateState  currentState={State.ToString()} connection={_streamConnection}");

        }

        private void DisconnectedHandler(DisconnectedMessage disconnectedMessage)
        {
            _logger.Info($"Disconnect message received");
            if (State == ConnectionState.DISCONNECTED || State == ConnectionState.CONNECTING)
            {
                _logger.Warn($"DisconnectedHandler will not be executed as currentState={State}");
            }

            if (disconnectedMessage.IDConnection !=null && disconnectedMessage.IDConnection != _streamConnection?.GetHashCode())
            {
                _logger.Warn($"DisconnectedHandler will not be executed as we are already in connection with connectionHash={_streamConnection?.GetHashCode()}, messageConnectionHash={disconnectedMessage?.IDConnection}");
            }

            
            Become(DisconnectedState);
            CloseConnection();
            NotifyDispatcherConnectionError();
            EstablishConnection(_connectionFactory);
            
        }

        private void DisconnecteOnDisconnectedHandler(DisconnectedMessage disconnectedMessage)
        {
            _logger.Warn($"Disconnect message On Disconnected state received");
        }

        private void ValidateConnection(ValidateConnectionMessage validateConnectionMessage)
        {
            //validate whether the reconnection was successful 
            _logger.InfoFormat("Starting validation for reconnection connHash={0}",
                _streamConnection?.GetHashCode().ToString() ?? "null");

            //in case the connection is swapped by RMQ library while the check is running
            var testConnection = _streamConnection;

            if (testConnection == null)
            {
                _logger.WarnFormat("Reconnection failed, connection has been disposed, the disconnection event needs to be raised");
                Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });

                return;
            }

            _logger.InfoFormat("Veryfing that connection is open ? {0}", testConnection.IsOpen);

            if (testConnection.IsOpen)
            {
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new ResetAllEchoesMessage());
                _logger.InfoFormat("Reconnection successful, disconnection event will not be raised");

                Self.Tell(new ValidationSucceededMessage());
            }
            else
            {
                _logger.Warn("Connection validation failed, connection is not open - calling CloseConnection() to dispose it");
                Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
            }
        }
        

        #endregion

        #region Consumer

        protected virtual void AddConsumerToQueue(IConsumer consumer)
        {
            
            if (consumer == null)
            {
                _logger.Warn("Method=AddConsumerToQueue Consumer is null");
                return;
            }
            _logger.Debug($"Method=AddConsumerToQueue triggered consumr={consumer.Id}");

            var queue = consumer.GetQueueDetails();

            if (string.IsNullOrEmpty(queue?.Name))
            {
                _logger.Warn("Method=AddConsumerToQueue Invalid queue details");
                return;
            }


            if (_streamConnection == null)
            {
                _logger.Warn($"Method=AddConsumerToQueue StreamConnection is null currentState={State.ToString()}");
                Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
                Stash.Stash();
                return;
            }

            if (!_streamConnection.IsOpen)
            {
                _logger.Warn($"Method=AddConsumerToQueue StreamConnection is closed currentState={State.ToString()}");
                Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
                Stash.Stash();
                return;
            }

            var model = _streamConnection.CreateModel();

            StreamSubscriber subscriber = null;

            try
            {
                subscriber = new StreamSubscriber(model, consumer, Dispatcher);
                subscriber.StartConsuming(queue.Name);
            }
            catch (Exception e)
            {
                if (subscriber != null)
                    subscriber.Dispose();
                timeoutCounter++;
                _logger.Warn($"Method=AddConsumerToQueue StartConsuming errored for consumerId={consumer.Id} {e}");
                if (timeoutCounter > TimeoutCounterLimit)
                    throw;
            }
            _logger.Debug($"Method=AddConsumerToQueue successfully executed consumr={consumer.Id}");
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (consumer == null)
                throw new ArgumentNullException("consumer");

            RemoveConsumerFromQueue(consumer);
        }

        protected virtual void RemoveConsumerFromQueue(IConsumer consumer)
        {
            var sub = Dispatcher.Ask(new RetrieveSubscriberMessage { Id = consumer.Id }).Result as IStreamSubscriber;
            if (sub != null)
                sub.StopConsuming();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _logger.Debug("Shutting down StreamController");
            _connectionCancellation.Cancel();
            CancelValidationMessages();
            Dispatcher.Tell(new DisposeMessage());
            Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });

            _logger.Info("StreamController correctly disposed");
        }

        #endregion

        #region Private messages
        private class ConnectedMessage
        {

        }

        public class DisconnectedMessage
        {
            public int? IDConnection { get; set; }
        }

        private class ValidateConnectionMessage
        {
        }

        private class ValidationStartMessage
        {
        }

        private class ValidationSucceededMessage
        {
        }

        private class ValidateStateMessage
        {

        }

        #endregion


    }
}

