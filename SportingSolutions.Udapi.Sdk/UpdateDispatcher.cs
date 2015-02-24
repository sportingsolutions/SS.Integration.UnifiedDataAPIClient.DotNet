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
using System.Threading.Tasks;
using log4net;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    internal class UpdateDispatcher : IDispatcher
    {
        private class ConsumerQueue
        {
            private readonly ConcurrentQueue<string> _updates;
            private readonly IConsumer _consumer;
            private volatile bool _disconnectRequested; // must be volatile
            private bool _isProcessing;
                
            public ConsumerQueue(IConsumer consumer)
            {
                _updates = new ConcurrentQueue<string>();
                _consumer = consumer;
                _disconnectRequested = false;
                _isProcessing = false;
            }

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
                            _consumer.OnStreamDisconnected();
                            break;
                        }

                        string message = null;
                        
                        _updates.TryDequeue(out message);

                        if (!string.IsNullOrEmpty(message))
                        {
                            try
                            {
                                if ("CONNECT".Equals(message))
                                    _consumer.OnStreamConnected();
                                else
                                    _consumer.OnStreamEvent(new StreamEventArgs(message));
                            }
                            catch { }
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

        private ConcurrentDictionary<string, ConsumerQueue> _consumers;
        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateDispatcher));

        public UpdateDispatcher()
        {
            _consumers = new ConcurrentDictionary<string, ConsumerQueue>();
        }

        #region IDispatcher Members

        public bool HasDestination(IConsumer consumer)
        {
            if(consumer == null || string.IsNullOrEmpty(consumer.Id))
                return false;

            return _consumers.ContainsKey(consumer.Id);
        }

        public void AddDestination(IConsumer consumer)
        {
            if(consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            var c = new ConsumerQueue(consumer);
            _consumers[consumer.Id] = c;
            c.Connect();

            _logger.DebugFormat("consumerId={0} added to the dispatcher, count={1}", consumer.Id, _consumers.Count);
        }

        public void RemoveDestination(IConsumer consumer)
        {
            if(consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            ConsumerQueue c;
            _consumers.TryRemove(consumer.Id, out c);

            if(c != null)
            {
                c.Disconnect();

                _logger.DebugFormat("consumerId={0} remove from the dispatcher, count={1}", consumer.Id, _consumers.Count);
            }
        }

        public void RemoveAll()
        {
            var tmp = _consumers;
            _consumers = new ConcurrentDictionary<string, ConsumerQueue>();
            
            ParallelOptions po = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount};

            _logger.DebugFormat("Sending disconnection to count={0} consumers", tmp.Values.Count);

            Parallel.ForEach(tmp.Values, po, x => x.Disconnect());

            _logger.Debug("All consumers are disconnected");
        }

        public bool DispatchMessage(string consumerId, string message)
        {
            if(string.IsNullOrEmpty(consumerId) || string.IsNullOrEmpty(message))
                return false;

            // note that TryGetValue is lock-free
            ConsumerQueue c = null; 
            if(!_consumers.TryGetValue(consumerId, out c) || c == null)
                return false;

            c.Add(message);

            return true;
        }

        #endregion
       
    }
}
