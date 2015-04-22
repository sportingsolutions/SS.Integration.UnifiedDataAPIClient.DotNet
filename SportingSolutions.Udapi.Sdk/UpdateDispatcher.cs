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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    internal class UpdateDispatcher : IDispatcher
    {
        #region Consumer Queue Internal

        private class ConsumerQueue
        {
            private static readonly ILog _logger = LogManager.GetLogger(typeof(ConsumerQueue));

            private readonly ConcurrentQueue<string> _updates;
            private volatile bool _disconnectRequested; // must be volatile
            private bool _isProcessing;
                
            public ConsumerQueue(IConsumer consumer)
            {
                _updates = new ConcurrentQueue<string>();
                _disconnectRequested = false;
                _isProcessing = false;
                Consumer = consumer;
            }

            public IConsumer Consumer { get; private set;}

            public void Add(string message)
            {
                _updates.Enqueue(message);
                Process();
            }

            public void Connect()
            {
                Add("CONNECT");
            }

            public void Disconnect()
            {
                _disconnectRequested = true;
                Process();
            }

            private void Process()
            {
              
                Task.Factory.StartNew(() =>
                {
                    lock (this)
                    {
                        if (_isProcessing)
                            return;

                        _isProcessing = true;
                    }

                    bool go = !_updates.IsEmpty || _disconnectRequested;

                    while (go)
                    {
                        if (_disconnectRequested)
                        {
                            if(UDAPI.Configuration.VerboseLogging)
                                _logger.DebugFormat("Going to send disconnection event for consumerId={0}", Consumer.Id);

                            Consumer.OnStreamDisconnected();
                            break;
                        }

                        string message = null;
                        
                        _updates.TryDequeue(out message);

                        if (!string.IsNullOrEmpty(message))
                        {
                            try
                            {
                                if ("CONNECT".Equals(message))
                                {
                                    if (UDAPI.Configuration.VerboseLogging)
                                        _logger.DebugFormat("Going to send connection event for consumerId={0}", Consumer.Id);

                                    Consumer.OnStreamConnected();
                                }
                                else
                                {
                                    if(UDAPI.Configuration.VerboseLogging)
                                        _logger.DebugFormat("Update arrived for consumerId={0}, pending={1}", Consumer.Id, _updates.Count);

                                    Consumer.OnStreamEvent(new StreamEventArgs(message));
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.Error("An error occured while pushing update for consumerId=" + Consumer.Id, e);
                            }
                        }

                        lock (this)
                        {
                            go = !_updates.IsEmpty || _disconnectRequested;
                            _isProcessing = go;
                        }
                    }
                });
            }
        }

        #endregion

        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateDispatcher));

        private ConcurrentDictionary<string, ConsumerQueue> _consumers;
        private readonly IEchoController _echosController;
        private readonly object _lock = new object();
        private bool _removeAllPending;

        public UpdateDispatcher()
        {
            _consumers = new ConcurrentDictionary<string, ConsumerQueue>();
            _echosController = new EchoController(this);

            _removeAllPending = false;

            _logger.DebugFormat("UpdateDispatcher initialised");
        }

        #region IDispatcher Members

        public bool HasConsumer(IConsumer consumer)
        {
            if(consumer == null || string.IsNullOrEmpty(consumer.Id))
                return false;

            return _consumers.ContainsKey(consumer.Id);
        }

        public void AddConsumer(IConsumer consumer)
        {
            if(consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            var c = new ConsumerQueue(consumer);
            _consumers[consumer.Id] = c;
            _echosController.AddConsumer(consumer);
            c.Connect();

            _logger.InfoFormat("consumerId={0} added to the dispatcher, count={1}", consumer.Id, ConsumersCount);
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if(consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            ConsumerQueue c = null;
            _consumers.TryRemove(consumer.Id, out c);

            try
            {
                if (c != null)
                {
                    c.Disconnect();

                    _logger.DebugFormat("consumerId={0} removed from the dispatcher, count={1}", consumer.Id, ConsumersCount);
                }
            }
            finally
            {
                _echosController.RemoveConsumer(c.Consumer);
            }
        }

        public void RemoveAll()
        {
            try
            {
                lock (_lock)
                {
                    if(_removeAllPending)
                        return;

                    _removeAllPending = true;
                }

                
                _logger.DebugFormat("Sending disconnection to count={0} consumers", _consumers.Count);

                foreach(var c in _consumers.Values)
                {
                    c.Disconnect();
                }

                _logger.Info("All consumers are disconnected");

            }
            finally
            {
                _echosController.RemoveAll();
                _consumers = new ConcurrentDictionary<string, ConsumerQueue>();

                lock(_lock)
                {
                    _removeAllPending  = false;
                    Monitor.PulseAll(_lock);
                }
            }
        }

        public bool DispatchMessage(string consumerId, string message)
        {

            if(string.IsNullOrEmpty(consumerId) || string.IsNullOrEmpty(message))
                return false;

            // is this an echo message?
            if(message.StartsWith("{\"Relation\":\"http://api.sportingsolutions.com/rels/stream/echo\""))
            {
                _echosController.ProcessEcho(consumerId);
                return true;
            }            


            if(UDAPI.Configuration.VerboseLogging)
                _logger.DebugFormat("Update arrived for consumerId={0}", consumerId);

            // note that TryGetValue is lock-free
            ConsumerQueue c = null; 
            if(!_consumers.TryGetValue(consumerId, out c) || c == null)
                return false;

            c.Add(message);

            return true;
        }

        public int ConsumersCount { get {  return _consumers.Count; } }

        public bool EnsureAvailability()
        {
            bool ok = true;
            lock(_lock)
            {
                if(_removeAllPending)
                {
                    ok = Monitor.Wait(_lock, UDAPI.Configuration.DisconnectionOperationTimeout);
                }
            }

            return ok;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _logger.DebugFormat("Disposing dispatcher");

            try
            {
                RemoveAll();
            }
            finally
            {
                _echosController.Dispose();
            }
        }

        #endregion
    }
}
