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
        private class EchoEntry
        {
            public IConsumer Consumer;
            public int EchosCountDown;
        }


        private readonly ILog _logger = LogManager.GetLogger(typeof(EchoController));
        private readonly ConcurrentDictionary<string, EchoEntry> _consumers; 
        private readonly Task _echoSender;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public EchoController()
        {
            Enabled = UDAPI.Configuration.UseEchos;
            _consumers = new ConcurrentDictionary<string, EchoEntry>();
            _cancellationTokenSource = new CancellationTokenSource();

            if(Enabled)
            {
                _echoSender = Task.Factory.StartNew(CheckEchos, _cancellationTokenSource.Token);
            }

            _logger.DebugFormat("EchoSender is {0}", Enabled ? "enabled" : "disabled");
        }

        public bool Enabled { get; private set; }

        public void AddConsumer(IConsumer consumer)
        {
            if(!Enabled || consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            _consumers[consumer.Id] = new EchoEntry 
                { 
                    Consumer = consumer, 
                    EchosCountDown = UDAPI.Configuration.MissedEchos 
                };

            _logger.DebugFormat("consumerId={0} added to echos manager", consumer.Id);
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (!Enabled || consumer == null || string.IsNullOrEmpty(consumer.Id))
                return;

            EchoEntry tmp;
            if(_consumers.TryRemove(consumer.Id, out tmp))
                _logger.DebugFormat("consumerId={0} removed from echos manager", consumer.Id);
        }

        public void RemoveAll()
        {
            if(!Enabled)
                return;

            _consumers.Clear();
        }

        public void ProcessEcho(string consumerId)
        {
            EchoEntry entry;
            if(!string.IsNullOrEmpty(consumerId) && _consumers.TryGetValue(consumerId, out entry))
            {
                if(UDAPI.Configuration.VerboseLogging)
                    _logger.DebugFormat("Resetting echo information for consumerId={0}", consumerId);

                entry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }
        }

        private void CheckEchos()
        {
            _logger.InfoFormat("Starting Echo task...");

            List<IConsumer> invalidConsumers = new List<IConsumer>();

            while(!_cancellationTokenSource.IsCancellationRequested)
            {
                foreach(var consumer in _consumers)
                {
                    int tmp = consumer.Value.EchosCountDown;
                    consumer.Value.EchosCountDown--;

                    if(tmp != UDAPI.Configuration.MissedEchos)
                    {
                        _logger.DebugFormat("consumerId={0} missed count={1} echos", consumer.Key, UDAPI.Configuration.MissedEchos - tmp);

                        if (tmp <= 1)
                        {
                            _logger.WarnFormat("consumerId={0} missed count={1} echos and it will be disconnected", consumer.Key, UDAPI.Configuration.MissedEchos);
                            invalidConsumers.Add(consumer.Value.Consumer);

                        }
                    }
                }


                // this wil force indirectly a call to EchoManager.RemoveConsumer(consumer)
                // for the invalid consumers
                StreamController.Instance.RemoveConsumers(invalidConsumers); 

                invalidConsumers.Clear();

                SendEchos();

                _cancellationTokenSource.Token.WaitHandle.WaitOne(UDAPI.Configuration.EchoWaitInterval);
            }

            _logger.InfoFormat("Echo task quitting...");
        }

        
        private void SendEchos()
        {
            foreach(var c in _consumers)
            {
                try
                {
                    c.Value.Consumer.SendEcho();
                    break;
                }
                catch (Exception e)
                {
                    _logger.Error("An error occured while trying to send echo-request", e);
                }
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
