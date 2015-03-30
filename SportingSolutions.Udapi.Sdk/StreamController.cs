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

namespace SportingSolutions.Udapi.Sdk
{
    internal class StreamController : IDisposable
    {

        internal enum ConnectionState
        {
            DISCONNECTED = 0,
            CONNECTING = 1,
            CONNECTED = 2
        }

        private static readonly ILog _logger = LogManager.GetLogger(typeof(StreamController));
        private static readonly object _lock = new object();
        private static readonly object _connectionLock = new object();
        private static StreamController _instance;


        private StreamSubscriber _consumer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private IModel _channel;
        private ConnectionFactory _connectionFactory;
        private IConnection _streamConnection;

        internal StreamController(IDispatcher dispatcher)
        {
            if(dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            Dispatcher = dispatcher;
            _cancellationTokenSource = new CancellationTokenSource();
            State = ConnectionState.DISCONNECTED;
        }

        private StreamController()
            : this (new UpdateDispatcher())
        {
        }


        public static StreamController Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new StreamController();
                    }
                }

                return _instance;
            }
        }

        public IDispatcher Dispatcher { get; internal set; }

        internal ConnectionState State { get; private set; }

        #region Connection management

        private void WaitForConnection(ConnectionFactory factory)
        {
            if (State != ConnectionState.CONNECTED)
            {
                // this prevent any other calles to call EstablishConnection()
                // EstablishConnection will wake up any sleeping threads when it finishes
                lock (_connectionLock)
                {
                    while (State == ConnectionState.CONNECTING)
                    {
                        Monitor.Wait(_connectionLock);
                    }

                    if (State == ConnectionState.CONNECTED || _cancellationTokenSource.IsCancellationRequested)
                        return;

                    State = ConnectionState.CONNECTING;
                }

                EstablishConnection(factory);
            }
        }

        private void Reconnect()
        {
            // prevent any others to restablish the connection
            lock (_connectionLock)
            {
                State = ConnectionState.CONNECTING;
            }

            if (_consumer != null)
            {
                _consumer.Dispose();
            }

            try
            {
                if (_streamConnection != null)
                    _streamConnection.Close();

            }
            catch { }

            try
            {
                Dispatcher.RemoveAll();

            }
            finally
            {
                EstablishConnection(_connectionFactory);
            }
        }

        private void EstablishConnection(ConnectionFactory factory)
        {
            // this method doesn't quit until
            // 1) a connection is established
            // 2) Dispose is called
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

                _logger.InfoFormat("Connecting to the streaming server");

                long attempt = 1;
                bool result = false;
                while (!_cancellationTokenSource.IsCancellationRequested && !result)
                {
                    _logger.DebugFormat("Establishing connection to the streaming server, attempt={0}", attempt);

                    try
                    {
                        _streamConnection = factory.CreateConnection();                        
                        _channel = _streamConnection.CreateModel();

                        _streamConnection.ConnectionShutdown += OnConnectionShutdown;
                        _consumer = new StreamSubscriber(Dispatcher);
                        newstate = ConnectionState.CONNECTED;
                        result = true;
                        
                        _logger.Info("Connection to the streaming server correctly established");
                        
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
                // wake up any sleeping threads
                lock (_connectionLock)
                {
                
                    State = newstate;

                    if (_connectionFactory == null)
                        _connectionFactory = factory;

                    Monitor.PulseAll(_connectionLock);
                }
            }
        }

        public virtual void Connect(string host, int port, string user, string password, string virtualHost)
        {
            var factory = new ConnectionFactory
            {
                RequestedHeartbeat = 5,
                HostName = host,
                Port = port,
                UserName = user,
                Password = password,
                VirtualHost = "/" + virtualHost
            };

            WaitForConnection(factory);
        }

        protected virtual void OnConnectionShutdown(IConnection connection, ShutdownEventArgs reason)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("There has been a streaming connection failure").AppendLine();
                stringBuilder.Append(reason);

                _logger.Error(stringBuilder.ToString());

                Reconnect();
            }
        }

        #endregion

        #region Consumer

        public virtual void AddConsumer(IConsumer consumer, int echoInterval, int echoMaxDelay)
        {
            if(consumer == null)
                throw new ArgumentNullException("consumer");

            QueueDetails queue = consumer.GetQueueDetails();
            if(queue == null || string.IsNullOrEmpty(queue.Name))
                throw new ArgumentNullException("consumer", "queue's name is not valid");

            Connect(queue.Host, queue.Port, queue.UserName, queue.Password, queue.VirtualHost);

            if (_cancellationTokenSource.IsCancellationRequested)
                throw new InvalidOperationException("StreamController is shutting down");

            if (State != ConnectionState.CONNECTED)
                throw new Exception("Connection is not open - cannot register a new consumer on queue=" + queue.Name);

            _logger.InfoFormat("Creating consumer for queue={0} id={1}", queue.Name, consumer.Id);


            // operations on IModel are not thread-safe
            lock (_lock)
            {
                if (Dispatcher.HasDestination(consumer))
                    throw new InvalidOperationException(string.Format("Consumer Id={0} already exists - cannot add twice", consumer.Id));

                var ctag = _channel.BasicConsume(queue.Name, true, consumer.Id, _consumer);
                _logger.InfoFormat("ctag=" + ctag);
                Dispatcher.AddDestination(consumer);
            }

            EnsureConsumerIsRunning();
            
        }

        public virtual void RemoveConsumer(IConsumer consumer)
        {
            if(consumer == null)
                throw new ArgumentNullException("consumer");

            try
            {
                lock (_lock)
                {
                    if(Dispatcher.HasDestination(consumer))
                        _channel.BasicCancel(consumer.Id);
                }
            }
            finally
            {
                Dispatcher.RemoveDestination(consumer);
            }
        }

        private void EnsureConsumerIsRunning()
        {
            _consumer.Start();
        }

        #endregion

        #region IDisposable Members

        public virtual void Shutdown()
        {
            _logger.Info("Shutting down StreamController");
            _cancellationTokenSource.Cancel();

            Dispatcher.RemoveAll();

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
            }

            _logger.Debug("Streaming channel closed");

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
            }

            _logger.Debug("Streaming connection closed");

            if (_consumer != null)
                _consumer.Dispose();

            _logger.InfoFormat("StreamController correctly disposed");
        }

        public void Dispose()
        {
            Shutdown();
        }

        #endregion

    }
}
