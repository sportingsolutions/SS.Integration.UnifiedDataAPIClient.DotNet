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
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using System.Collections.Generic;

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
        
        private static readonly object _lock = new object();
        private static readonly object _connectionLock = new object();

        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private IModel _channel;
        private IConnection _streamConnection;
        private volatile ConnectionState _state;
        private bool _isLogicConnectionValid;

        internal StreamController(IDispatcher dispatcher)
        {
            if(dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcher;
            _cancellationTokenSource = new CancellationTokenSource();
            
            State = ConnectionState.DISCONNECTED;
            _isLogicConnectionValid = true;


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

        /// <summary>
        ///
        ///     Returns the StreamSubscriber. It is responsible
        ///     for reading AMPQ messages from the connection
        ///     and dispatch them to the consumers using
        ///     the consumers' Ids.
        /// 
        /// </summary>
        protected StreamSubscriber Consumer { get; set; }

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

        #region Connection management

        protected virtual void CloseConnection()
        {
            // this prevents any new consumer
            // to be added while we clean up
            lock (_lock)
            {
                _isLogicConnectionValid = false;
            }

            try
            {
                // this prevents any consumer to establish a new connection
                // (and therefore, replacing the new Consumer variable with 
                // a new object)
                lock (_connectionLock)
                {
                    if (Consumer != null)
                    {
                        Consumer.SubscriberShutdown -= OnModelShutdown;
                        Consumer.Dispose();
                        Consumer = null;
                    }

                    try
                    {
                        if (_streamConnection != null)
                            _streamConnection.Close();
                    }
                    catch { }

                    OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
                }

                // this could run outside the lock, as _isLogicConnectionValid is 
                // still set to false, hence, no consumers can be added
                Dispatcher.RemoveAll();
            }
            finally
            {
                lock (_lock)
                {
                    _isLogicConnectionValid = true;
                }
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
                        _channel = _streamConnection.CreateModel();

                        _logger.Info("Connection to the streaming server correctly established");

                        Consumer = new StreamSubscriber(Dispatcher);

                        // this is the main event raised if something went wrong with the AMPQ SDK
                        Consumer.SubscriberShutdown += OnModelShutdown;    
                        newstate = ConnectionState.CONNECTED;
                        result = true;
                        
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Unable to connect to streaming server...Retrying", ex);
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

        public void Connect(IConsumer consumer)
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

                QueueDetails queue = consumer.GetQueueDetails();
                if (queue == null || string.IsNullOrEmpty(queue.Name))
                {
                    OnConnectionStatusChanged(ConnectionState.DISCONNECTED);
                    throw new Exception("queue's name is not valid for consumerId=" + consumer.Id);
                }


                var factory = new ConnectionFactory {
                    RequestedHeartbeat = 5,
                    HostName = queue.Host,
                    Port = queue.Port,
                    UserName = queue.UserName,
                    Password = queue.Password,
                    VirtualHost = "/" + queue.VirtualHost // this is not used anymore, we keep it for retro-compatibility
                };

                EstablishConnection(factory);
            }            
        }

        protected virtual void OnModelShutdown(object sender, ShutdownEventArgs reason)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("There has been a problem with the streaming connection").AppendLine();
                stringBuilder.Append(reason);

                _logger.Error(stringBuilder.ToString());

                CloseConnection();
            }
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

            AddConsumerToQueue(consumer);

            EnsureConsumerIsRunning();
            
        }

        protected virtual void AddConsumerToQueue(IConsumer consumer)
        {

            // this is quite a bottle-neck, however, operations 
            // on IModel are not thread-safe so a lock is necessary.
            //
            // As there could be many consumers waiting to execute
            // BasicConsume (just think about after a disconnection)
            // GetQueueDetails must be inside the lock as well, otherwise
            // by the time the thread gets to call BasicConsume, the queue
            // created by GetQueueDetails might have disappeard due
            // the expire settings

            lock (_lock)
            {
                if (!_isLogicConnectionValid)
                    throw new InvalidOperationException("Cannot accept consumers at the moment");

                if (Dispatcher.HasConsumer(consumer))
                    throw new InvalidOperationException(string.Format("consumerId={0} already exists - cannot add it twice", consumer.Id));                

                QueueDetails queue = consumer.GetQueueDetails();
                if (queue == null || string.IsNullOrEmpty(queue.Name))
                    throw new Exception("Invalid queue details");

                _channel.BasicConsume(queue.Name, true, consumer.Id, Consumer);

                Dispatcher.AddConsumer(consumer);
            }
        }
     
        public void RemoveConsumer(IConsumer consumer)
        {
            if(consumer == null)
                throw new ArgumentNullException("consumer");

            RemoveConsumerFromQueue(consumer, true);
        }

        internal void RemoveConsumers(IEnumerable<IConsumer> consumers)
        {
            if(consumers == null)
                return;

            /* 
             *  First thing to do is to remove the consumer from the dispatcher.
             *  
             *  This allows us to:
             *  1) immediately send the disconnection event
             *  2) prevent to send other messages (note we can still receive messages
             *     as BasicCancel has not yet been called)
             * 
             *  Also note that RemoveConsumer behaves as an async method
             *  so it returns pretty quickly
             */

            foreach (var c in consumers)
            {
                lock (_lock)
                {
                    if (!_isLogicConnectionValid)
                        break;

                    try
                    {
                        _channel.BasicCancel(c.Id);
                    }
                    finally
                    {
                        Dispatcher.RemoveConsumer(c);
                    }
                }
            }
        }
        
        protected virtual void RemoveConsumerFromQueue(IConsumer consumer, bool checkIfExists)
        {
            if (checkIfExists && !Dispatcher.HasConsumer(consumer))
                return;

            lock(_lock)
            {
                if (!_isLogicConnectionValid)
                    return;

                try
                {
                    _channel.BasicCancel(consumer.Id);
                }
                finally
                {
                    Dispatcher.RemoveConsumer(consumer);
                }
            }
        }

        private void EnsureConsumerIsRunning()
        {
            Consumer.Start();
        }

        #endregion

        #region IDisposable Members

        public virtual void Shutdown()
        {
            _logger.Info("Shutting down StreamController");
            _cancellationTokenSource.Cancel();

            Dispatcher.Dispose();

            if (_channel != null)
            {
                try
                {
                    _channel.Close();
                }
                catch (Exception e)
                {
                    // don't bubble it up
                    _logger.Warn(
                        string.Format("An error occured while trying to close the streaming channel"), e);
                }

                _logger.Debug("Streaming channel closed");
            }

            if (_streamConnection != null)
            {
                try
                {
                    _streamConnection.Close();
                }
                catch (Exception e)
                {
                    // don't bubble it up
                    _logger.Warn(
                        string.Format("An error occured while trying to close the streaming connection"), e);
                }

                _logger.Debug("Streaming connection closed");
            }

            if (Consumer != null)
            {
                Consumer.SubscriberShutdown -= OnModelShutdown;
                Consumer.Dispose();
                Consumer = null;
            }

            _logger.InfoFormat("StreamController correctly disposed");
        }

        public void Dispose()
        {
            Shutdown();
        }

        #endregion

    }
}
