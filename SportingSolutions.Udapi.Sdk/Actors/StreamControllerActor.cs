using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public const int NewConsumerErrorLimit = 5;
        private static ICancelable _validateCancellation;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(StreamControllerActor));
        private volatile ConnectionState _state;
        private readonly ICancelable _connectionCancellation = new Cancelable(Context.System.Scheduler);
        private StreamSubscriber _subscriber = null;

	    protected IConnection _streamConnection;
	    private string _queue;


		public StreamControllerActor(IActorRef dispatcherActor)
        {
            Dispatcher = dispatcherActor ?? throw new ArgumentNullException("dispatcher");
            //Start in Disconnected state
            DisconnectedState();

            AutoReconnect = UDAPI.Configuration.AutoReconnect;
            CancelValidationMessages();
            _validateCancellation= Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(10000, 10000, Self, new ValidateStateMessage(), Self);

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
            Receive<SubriberErroredMessage>(x => SubriberErroredHandler());
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateStateMessage>(x => ValidateState(x));
            Receive<DisconnectedMessage>(x => DisconnecteOnDisconnectedHandler(x));

            State = ConnectionState.DISCONNECTED;
        }

	    private void SubriberErroredHandler()
	    {
		    _logger.Info("SubriberErroredMessage received");
			ForceReconnection();

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
            Receive<SubriberErroredMessage>(x => Stash.Stash());
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
            Receive<NewConsumerMessage>(x =>
            {
                if (ValidateNewConsumerCanBeProcessed(x.Consumer))
                {
                    NewConsumerHandler(x);
                }
                else
                {
                    Stash.Stash();
                }
            });
            Receive<DisconnectedMessage>(x => DisconnectedHandler(x));
            Receive<SubriberErroredMessage>(x => SubriberErroredHandler());
            Receive<DisposeMessage>(x => Dispose());
            Receive<ValidateConnectionMessage>(x => ValidateConnection(x));
            Receive<ValidateStateMessage>(x => ValidateState(x));

            State = ConnectionState.CONNECTED;
            Stash.UnstashAll();
            
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



        private DisconnectedMessage DefaultDisconnectedMessage => new DisconnectedMessage {IDConnection = _streamConnection?.GetHashCode()};


        

        private void NewConsumerHandler(NewConsumerMessage newConsumerMessage)
        {
			_logger.Debug($"Method=ProcessNewConsumer triggered fixtureId={newConsumerMessage.Consumer?.Id} currentState={State.ToString()} connectionStatus={ConnectionStatus}");
	        Dispatcher.Tell(new NewConsumerMessage { Consumer = newConsumerMessage.Consumer });
	        _logger.Debug($"Method=ProcessNewConsumer successfully executed fixtureId={newConsumerMessage.Consumer.Id}"); 
        }
		
        
        

	    private bool ResumeConsuming()
	    {
		    IModel model;
		    try
		    {
			    model = _streamConnection.CreateModel();
		    }
		    catch (Exception e)
		    {
			    _logger.Warn($"Method=ProcessNewConsumer CreateModel errored   {e}");
			    ForceReconnection();
				return false;
		    }

		    _subscriber?.StopConsuming();

		    _subscriber = new StreamSubscriber(model, Dispatcher);

		    var resumed  = _subscriber.ResumeConsuming(_queue);
			if (resumed)
				Dispatcher.Tell(new ReconsumedQueueMessage());
		    return resumed;
	    }

	    private bool StartConsuming()
	    {
		    IModel model;
		    try
		    {
			    model = _streamConnection.CreateModel();
		    }
		    catch (Exception e)
		    {
			    _logger.Warn($"Method=ProcessNewConsumer CreateModel errored   {e}");
			    ForceReconnection();
				return false;
		    }

		    _subscriber?.StopConsuming();
			_subscriber = new StreamSubscriber(model, Dispatcher);
		    _queue = _subscriber.StartConsuming();
		    return _queue != null;
	    }

		private bool ValidateNewConsumerCanBeProcessed(IConsumer consumer)
        {
            if (consumer == null)
            {
                _logger.Warn("Method=ProcessNewConsumer Consumer is null");
                return false;
            }

            if (_streamConnection == null || !_streamConnection.IsOpen)
            {
                _logger.Warn(
                    $"Method=ProcessNewConsumer connectionStatus={ConnectionStatus} {(_streamConnection == null ? "this should not happening" : "")}");


               DisconnectedHandler(DefaultDisconnectedMessage);
                

                return false;
            }

            return true;
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
                    _streamConnection.ConnectionShutdown -= OnConnectionShutdown;
                    if (_streamConnection.IsOpen)
                    {
                        _streamConnection.Close();
                        _logger.Debug("Connection Closed");
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
                Dispatcher.Tell(new DisconnectionAccuredMessage());
            }
            catch (Exception e)
            {
                _logger.Warn($"Failed to tell diapstcher DisconnectionAccuredMessage diapstcher={Dispatcher} {e}");
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
	                CloseConnection();
					_streamConnection = factory.CreateConnection();
                    _streamConnection.ConnectionShutdown += OnConnectionShutdown;

                    _logger.Info("Connection to the streaming server correctly established");

	                try
	                {
		                _logger.Info("Starting Consuming");
						if (_queue == null || !ResumeConsuming())
							StartConsuming();
		                Self.Tell(new ConnectedMessage());
		                return;

	                }
	                catch (Exception ex)
	                {
		                _logger.Error("Error start consuming the queue", ex);
		                CloseConnection();
						Thread.Sleep(100);
	                }


				}
                catch (Exception ex)
                {
                    _logger.Error("Error connecting to the streaming server", ex);
                    Thread.Sleep(100);
                }

	            

				attempt++;
            }

            _logger.Warn("EstablishConnection connection Cancelled");

            State = ConnectionState.DISCONNECTED;


        }

        

        private void GetQueueDetailsAndEstablisConnection(IConsumer consumer)
        {
            //_logger.Debug($"GetQueueDetailsAndEstablisConnection triggered state={State} isCancellationRequested={_connectionCancellation.IsCancellationRequested}");


            if (State == ConnectionState.CONNECTED || State == ConnectionState.CONNECTING ||
                _connectionCancellation.IsCancellationRequested)
            {
                //_logger.Info($"GetQueueDetailsAndEstablisConnection will not be executed state={State} isCancellationRequested={_connectionCancellation.IsCancellationRequested}");
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
                    var e = new Exception("queue's name is not valid for fixtureId=" + consumer.Id);
                    ConnectionError = e;
                    throw e;
                }
            }
            catch (Exception e)
            {
                _logger.Error("Error acquiring queue details for fixtureId=" + consumer.Id, e);
                ConnectionError = e;
                throw;
            }

            //_logger.Info($"ConnectionFactory h={queue.Host} u={queue.UserName} p={queue.Password} ch={queue.VirtualHost}");

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
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(DefaultDisconnectedMessage);
            }
            else
            {
                SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new ValidationStartMessage());
            }
        }

        private void ValidateState(ValidateStateMessage validateStateMessage)
        {
            var message = $"Method=ValidateState  currentState={State.ToString()} connectionStatus={ConnectionStatus} ";
            
            if (NeedRaiseDisconnect)
            {
                _logger.Warn($"{message} disconnected event will be raised");
                DisconnectedHandler(DefaultDisconnectedMessage);
            }
            else
            {
                _logger.Debug(message);
            }
        }

        private bool NeedRaiseDisconnect => State == ConnectionState.CONNECTED && (_streamConnection == null || !_streamConnection.IsOpen);

        private string ConnectionStatus => _streamConnection == null ? "NULL" : (_streamConnection.IsOpen ? "open" : "closed");

        private void DisconnectedHandler(DisconnectedMessage disconnectedMessage )
        {
            _logger.Info($"DisconnectedMessage received");
            if (State == ConnectionState.DISCONNECTED || State == ConnectionState.CONNECTING)
            {
                _logger.Warn($"DisconnectedHandler will not be executed as currentState={State}");
				return;
            }

            if (disconnectedMessage.IDConnection !=null && disconnectedMessage.IDConnection != _streamConnection?.GetHashCode())
            {
                _logger.Warn($"DisconnectedHandler will not be executed as we are already in connection with connectionHash={_streamConnection?.GetHashCode()}, messageConnectionHash={disconnectedMessage?.IDConnection}");
				return;
            }
            
            ForceReconnection();
        }

	    private void ForceReconnection()
	    {
		    Become(DisconnectedState);
		    CloseConnection();
		    NotifyDispatcherConnectionError();
		    EstablishConnection(_connectionFactory);
	    }

	    private void DisconnecteOnDisconnectedHandler(DisconnectedMessage disconnectedMessage)
        {
            _logger.Warn($"Disconnect message On Disconnected state received messageConnectionHash={disconnectedMessage.IDConnection}");
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

        
        

        #region IDisposable Members

        public void Dispose()
        {
            _logger.Debug("Shutting down StreamController");
            _connectionCancellation.Cancel();
            CancelValidationMessages();
            Dispatcher.Tell(new DisposeMessage());
            Self.Tell(new DisconnectedMessage { IDConnection = _streamConnection?.GetHashCode() });
			CloseConnection();
	        if (_subscriber != null)
	        {
				_subscriber.StopConsuming();
		        _subscriber = null;
			}


			_logger.Info("StreamController correctly disposed");
        }

	    

	    #endregion

        #region Private messages
        public class ConnectedMessage
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

