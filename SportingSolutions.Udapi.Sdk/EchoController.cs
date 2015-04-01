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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    internal class EchoController : IEchoController
    {
        
        private readonly ILog _logger = LogManager.GetLogger(typeof(EchoController));
        private readonly ConcurrentDictionary<string, IConsumer> _consumers; 
        private readonly ConcurrentDictionary<string, int> _counters;
        private readonly Task _echoSender;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public EchoController(IDispatcher dispatcher)
        {
            if(dispatcher == null) throw new ArgumentNullException("dispatcher");


            Dispatcher = dispatcher;
            Enabled = UDAPI.Configuration.UseEchos;
            _consumers = new ConcurrentDictionary<string, IConsumer>();
            _counters = new ConcurrentDictionary<string, int>();
            _cancellationTokenSource = new CancellationTokenSource();

            if(Enabled)
            {
                _echoSender = Task.Factory.StartNew(CheckEchos, _cancellationTokenSource.Token);
            }

            _logger.DebugFormat("EchoSender is {0}", Enabled ? "enabled" : "disabled");
        }

        public bool Enabled { get; private set; }

        public IDispatcher Dispatcher { get; private set; }

        public void AddConsumer(IConsumer consumer)
        {
            if(!Enabled || consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            _consumers[consumer.Id] = consumer;
            _counters[consumer.Id] = UDAPI.Configuration.MissedEchos;
            _logger.DebugFormat("consumerId={0} added to echos manager", consumer.Id);
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (!Enabled || consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            IConsumer tmp;
            int c;
            _consumers.TryRemove(consumer.Id, out tmp);
            _counters.TryRemove(consumer.Id, out c);
            _logger.DebugFormat("consumerId={0} removed from echos manager", consumer.Id);
        }

        public void RemoveAll()
        {
            if(!Enabled)
                return;

            _consumers.Clear();
            _counters.Clear();
        }

        public void ProcessEcho(string consumerId)
        {
            if(!string.IsNullOrEmpty(consumerId) && _counters.ContainsKey(consumerId))
            {
                if(UDAPI.Configuration.VerboseLogging)
                    _logger.DebugFormat("Resetting echo information for consumerId={0}", consumerId);

                _counters[consumerId] = UDAPI.Configuration.MissedEchos;
            }
        }

        private void CheckEchos()
        {
            _logger.InfoFormat("Starting Echo task");

            List<IConsumer> invalidConsumers = new List<IConsumer>();

            while(!_cancellationTokenSource.IsCancellationRequested)
            {

                foreach(var consumer in _counters.Keys)
                {
                    int tmp = 0;
                    _counters.AddOrUpdate(consumer, 0, (key, oldValue) => {tmp = oldValue; return oldValue - 1;});

                    if(tmp != UDAPI.Configuration.MissedEchos)
                    {
                        _logger.DebugFormat("consumerId={0} missed count={1} echos", consumer, UDAPI.Configuration.MissedEchos - tmp);

                        if (tmp <= 1)
                        {
                            _logger.WarnFormat("consumerId={0} missed count={1} echos", consumer, UDAPI.Configuration.MissedEchos);
                            invalidConsumers.Add(_consumers[consumer]);
                        }
                    }
                }


                DisconnectConsumers(invalidConsumers);

                invalidConsumers.Clear();

                SendEchos();

                _cancellationTokenSource.Token.WaitHandle.WaitOne(UDAPI.Configuration.EchoWaitInterval);
            }

            _logger.InfoFormat("Echo task job done");
        }

        private void DisconnectConsumers(IEnumerable<IConsumer> consumers)
        {
            
            foreach(var consumer in consumers)
            {
                _logger.WarnFormat("consumerId={0} will be disconnected due missed echos", consumer.Id);
                RemoveConsumer(consumer);
                Dispatcher.RemoveConsumer(consumer);
            }
        }

        private void SendEchos()
        {
            // we just need one client
            foreach(var consumer in _consumers.Values)
            {
                try
                {
                    consumer.SendEcho();
                }
                catch (Exception e)
                {                    
                    _logger.Error("An error occured while trying to send echo-request", e);
                }
                break;
            }
        }

        public void Dispose()
        { 
            _logger.DebugFormat("Disposing EchoSender");
            _cancellationTokenSource.Cancel();

            RemoveAll();

            if(_echoSender != null)
                _echoSender.Wait();

            _logger.InfoFormat("EchoSender correctly disposed");
        }
    }
}
