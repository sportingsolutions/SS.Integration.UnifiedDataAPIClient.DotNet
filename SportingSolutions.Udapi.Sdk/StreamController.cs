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
using System.Collections.Generic;
using System.Text;
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
        private static readonly object _lock = new object();
        private static readonly object _connectionLock = new object();

        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private IModel _channel;
        private IConnection _streamConnection;
        private volatile ConnectionState _state;


        internal StreamController(IDispatcher dispatcher)
        {
            if(dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcher;
            _cancellationTokenSource = new CancellationTokenSource();
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
            // this prevents any others to restablish the connection
            lock (_connectionLock)
            {
                State = ConnectionState.DISCONNECTED;
            }

            if (Consumer != null)
            {
                Consumer.Dispose();
                Consumer.SubscriberShutdown -= OnModelShutdown;
                Consumer = null;
            }

            try
            {
                if (_streamConnection != null)
                    _streamConnection.Close();
            }
            catch { }

            Dispatcher.RemoveAll();
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

        public void Connect(string host, int port, string user, string password, string virtualHost)
        {
            if (State != ConnectionState.CONNECTED)
            {
                // this prevents any other execution of EstablishConnection().
                // EstablishConnection() wakes up any sleeping threads when it finishes
                lock (_connectionLock)
                {
                    while (State == ConnectionState.CONNECTING)
                    {
                        Monitor.Wait(_connectionLock); // wait until the connection is established
                    }

                    if (State == ConnectionState.CONNECTED || _cancellationTokenSource.IsCancellationRequested)
                        return;

                    State = ConnectionState.CONNECTING;
                }

                var factory = new ConnectionFactory 
                {
                    RequestedHeartbeat = 5,
                    HostName = host,
                    Port = port,
                    UserName = user,
                    Password = password,
                    VirtualHost = "/" + virtualHost // this is not used anymore, we keep it for retro-compatibility
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

            // wait for any disconnection operations if necessary...
            // note that this call is what ensure that the dispatcher
            // doesn't get itself in a corrupted state (i.e. Adding a consumer
            // when a Disptacher.RemoveAll() is called)
            if(!Dispatcher.EnsureAvailability())
                throw new Exception("IDispatcher is not yet ready - consumerId=" + consumer.Id + " not registred");

            // GetQueueDetails() calls the UDAPI service....it is here that 
            // the AMPQ queue is created on the server side!
            //
            // Dispatcher.EnsumeAvailability() makes sure that we don't have to
            // wait for any disconnection operations (i.e. the connection went down)
            // between the GetQueueDetails() and BasicConsume()

            QueueDetails queue = consumer.GetQueueDetails();
            if(queue == null || string.IsNullOrEmpty(queue.Name))
                throw new ArgumentNullException("consumer", "queue's name is not valid");

            Connect(queue.Host, queue.Port, queue.UserName, queue.Password, queue.VirtualHost);

            if (_cancellationTokenSource.IsCancellationRequested)
                throw new InvalidOperationException("StreamController is shutting down");

            if (State != ConnectionState.CONNECTED)
                throw new Exception("Connection is not open - cannot register a new consumer on queue=" + queue.Name);

            _logger.InfoFormat("Creating consumer for queue={0} consumerId={1}", queue.Name, consumer.Id);

            AddConsumerToQueue(consumer, queue.Name);

            EnsureConsumerIsRunning();
            
        }

        protected virtual void AddConsumerToQueue(IConsumer consumer, string queueName)
        {
            if (Dispatcher.HasConsumer(consumer))
                throw new InvalidOperationException(string.Format("consumerId={0} already exists - cannot add it twice", consumer.Id));

            // operations on IModel are not thread-safe
            lock (_lock)
            {
                _channel.BasicConsume(queueName, true, consumer.Id, Consumer);
                Dispatcher.AddConsumer(consumer);
            }
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if(consumer == null)
                throw new ArgumentNullException("consumer");

            try
            {
                RemoveConsumerFromQueue(consumer, true);
            }
            finally
            {
                Dispatcher.RemoveConsumer(consumer);
            }
        }

        public void RemoveConsumers(IEnumerable<IConsumer> consumers)
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
            foreach(var c in consumers)
            {
                Dispatcher.RemoveConsumer(c);
            }


            /* 
             * Only now that we have sent out the disconnection events
             * we can spend some time by sending the BasicCancel().
             * 
             * As BasicCancel requires to send data to the rabbit server
             * it might take a while (and it can raise exceptions, so
             * pay attention when locking)
             * 
             * Moreover, no point in sending out BasicCancel is the 
             * connection is down.
             * 
             */


            bool performRemove = false;

            if(Monitor.TryEnter(_connectionLock, 3000))
            {
                performRemove = State == ConnectionState.CONNECTED; 
                Monitor.Exit(_connectionLock);
            }

            if (performRemove)
            {

                foreach (var c in consumers)
                {
                    try
                    {
                        RemoveConsumerFromQueue(c, false);
                    }
                    catch (Exception e)
                    {
                        if (UDAPI.Configuration.VerboseLogging)
                            _logger.WarnFormat("Error while removing consumerId=" + c.Id + " from the streaming queue", e);
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
                _channel.BasicCancel(consumer.Id);
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
                Consumer.Dispose();
                Consumer.SubscriberShutdown -= OnModelShutdown;
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
