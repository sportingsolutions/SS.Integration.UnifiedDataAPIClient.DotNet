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
using System.Threading;
using RabbitMQ.Client;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
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
    internal class StreamController : IDisposable
    {

        static StreamController()
        {
            Instance = new StreamController();
        }

        internal enum ConnectionState
        {
            DISCONNECTED = 0,
            CONNECTING = 1,
            CONNECTED = 2
        }

        private static readonly ILog _logger = LogManager.GetLogger(typeof(StreamController));
        private static readonly TimeSpan _lockingTimeout = TimeSpan.FromSeconds(3);


        private static readonly object _connectionLock = new object();
        private readonly ReaderWriterLock _lock = new ReaderWriterLock();
        private bool _canPerformChannelOperations;

        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private IConnection _streamConnection;
        private volatile ConnectionState _state;

        internal StreamController(IDispatcher dispatcher)
        {
            if(dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcher;
            _cancellationTokenSource = new CancellationTokenSource();
            _canPerformChannelOperations = true;
            State = ConnectionState.DISCONNECTED;

            _logger.DebugFormat("StreamController initialised");
        }

        private StreamController()
            : this(new UpdateDispatcher()) { }


        public static StreamController Instance { get; internal set; }

        /// <summary>
        /// 
        ///     Returns the IDispatcher object that is responsible
        ///     of dispatching messages to the consumers.
        /// 
        /// </summary>
        internal IDispatcher Dispatcher { get; private set; }

        internal ConnectionState State 
        { 
            get 
            { 
                // as it is volatile, reading this property is thread-safe
                return _state; 
            } 
            private set 
            { 
                _state = value; 
            } 
        }

        internal bool CanPerformChannelOperations
        {
            get
            {
                bool tmp;
                _lock.AcquireReaderLock(_lockingTimeout);
                tmp = _canPerformChannelOperations;
                _lock.ReleaseReaderLock();
                return tmp;
            }
            set
            {
                _lock.AcquireWriterLock(_lockingTimeout);
                _canPerformChannelOperations = value;
                _lock.ReleaseWriterLock();
            }
        }

        #region Connection management

        protected virtual void CloseConnection()
        {

            CanPerformChannelOperations = false;


            // this prevents any consumer to establish a new connection
            // (and therefore, replacing the new Consumer variable with 
            // a new object)
            lock (_connectionLock)
            {
                try
                {
                    if (_streamConnection != null)
                    {
                        _streamConnection.ConnectionShutdown -= OnConnectionShutdown;
                        _streamConnection.Close();
                        _streamConnection = null;
                    }
                }
                catch { }

                OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
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

            ConnectionState newstate = ConnectionState.DISCONNECTED;

            try
            {
                _logger.DebugFormat("Connecting to the streaming server");

                long attempt = 1;
                bool result = false;
                while (!_cancellationTokenSource.IsCancellationRequested && !result)
                {
                    _logger.DebugFormat("Establishing connection to the streaming server, attempt={0}", attempt);

                    try
                    {
                        _streamConnection = factory.CreateConnection();
                        _streamConnection.ConnectionShutdown += OnConnectionShutdown;

                        _logger.Info("Connection to the streaming server correctly established");

                        newstate = ConnectionState.CONNECTED;
                        result = true;
                        
                        CanPerformChannelOperations = true;

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

            if (State != ConnectionState.CONNECTED)
            {
                // this prevents any other execution of EstablishConnection().
                // EstablishConnection() wakes up any sleeping threads when it finishes

                lock (_connectionLock)
                {
                    while (State == ConnectionState.CONNECTING)
                    {
                        // wait until the connection is established
                        Monitor.Wait(_connectionLock); 
                    }

                    if (State == ConnectionState.CONNECTED || _cancellationTokenSource.IsCancellationRequested)
                        return;

                    State = ConnectionState.CONNECTING;
                }


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
                        throw new Exception("queue's name is not valid for consumerId=" + consumer.Id);   
                    }
                }
                catch (Exception e)
                {
                    _logger.Error("Error acquiring queue details for consumerId=" + consumer.Id, e);
                    OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
                    throw;
                }


                var factory = new ConnectionFactory {
                    RequestedHeartbeat = UDAPI.Configuration.AMQPMissedHeartbeat,
                    HostName = queue.Host,
                    Port = queue.Port,
                    UserName = queue.UserName,
                    Password = queue.Password,
                    VirtualHost = "/" + queue.VirtualHost // this is not used anymore, we keep it for retro-compatibility
                };

                EstablishConnection(factory);
            }            
        }

        protected virtual void OnConnectionShutdown(object sender, ShutdownEventArgs sea)
        {            
            _logger.Error("The AMQP connection was shutdown: " + sea);
            CloseConnection();
        }

        protected virtual void OnConnectionStatusChanged(ConnectionState newState)
        {
            // wake up any sleeping threads
            lock (_connectionLock)
            {
                State = newState;
                Monitor.PulseAll(_connectionLock);
            }
        }

        #endregion

        #region Consumer

        public void AddConsumer(IConsumer consumer, int echoInterval, int echoMaxDelay)
        {
            if(consumer == null)
                throw new ArgumentNullException("consumer");

            // Note that between Connect() and AddConsumerToQueue()
            // error could be raised (i.e. connection went down), 
            // so by the time we reach AddConsumerToQueue(),
            // nothing can be assumed

            Connect(consumer);

            if (_cancellationTokenSource.IsCancellationRequested)
                throw new InvalidOperationException("StreamController is shutting down");

            if (State != ConnectionState.CONNECTED)
                throw new Exception("Connection is not open - cannot register consumerId=" + consumer.Id);

            if(Dispatcher.HasSubscriber(consumer.Id))
                throw new InvalidOperationException("consumerId=" + consumer.Id + " cannot be registred twice");

            AddConsumerToQueue(consumer);            
        }

        protected virtual void AddConsumerToQueue(IConsumer consumer)
        {

            QueueDetails queue = consumer.GetQueueDetails();
            if (queue == null || string.IsNullOrEmpty(queue.Name))
                throw new Exception("Invalid queue details");


            if(!CanPerformChannelOperations)
                throw new InvalidOperationException("Cannot accept new consumers at the moment");

            var model = _streamConnection.CreateModel();    
            var subscriber = new StreamSubscriber(model, consumer, Dispatcher);
            subscriber.StartConsuming(queue.Name);
        }
           
        public void RemoveConsumer(IConsumer consumer)
        {
            if(consumer == null)
                throw new ArgumentNullException("consumer");

            RemoveConsumerFromQueue(consumer);
        }
        
        protected virtual void RemoveConsumerFromQueue(IConsumer consumer)
        {
            if(!CanPerformChannelOperations)
                return;

            var sub = Dispatcher.GetSubscriber(consumer.Id);
            if(sub != null)
                sub.StopConsuming();
        }

        #endregion

        #region IDisposable Members

        public virtual void Shutdown()
        {
            _logger.Debug("Shutting down StreamController");
            _cancellationTokenSource.Cancel();

            Dispatcher.Dispose();

            CloseConnection();

            _logger.Info("StreamController correctly disposed");
        }

        public void Dispose()
        {
            Shutdown();
        }

        #endregion

    }
}
