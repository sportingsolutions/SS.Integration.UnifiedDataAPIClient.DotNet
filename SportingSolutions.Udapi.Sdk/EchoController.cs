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
            public IStreamSubscriber Subscriber;
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

        public void AddConsumer(IStreamSubscriber subscriber)
        {
            if (!Enabled || subscriber == null)
                return;

            _consumers[subscriber.Consumer.Id] = new EchoEntry 
                {
                    Subscriber = subscriber, 
                    EchosCountDown = UDAPI.Configuration.MissedEchos 
                };

            _logger.DebugFormat("consumerId={0} added to echos manager", subscriber.Consumer.Id);
        }

        public void RemoveConsumer(IStreamSubscriber subscriber)
        {
            if (!Enabled || subscriber == null)
                return;

            EchoEntry tmp;
            if (_consumers.TryRemove(subscriber.Consumer.Id, out tmp))
                _logger.DebugFormat("consumerId={0} removed from echos manager", subscriber.Consumer.Id);
        }

        public void RemoveAll()
        {
            if(!Enabled)
                return;

            _consumers.Clear();
        }

        public void ProcessEcho(string subscriberId)
        {
            EchoEntry entry;
            if (!string.IsNullOrEmpty(subscriberId) && _consumers.TryGetValue(subscriberId, out entry))
            {
                if(UDAPI.Configuration.VerboseLogging)
                    _logger.DebugFormat("Resetting echo information for consumerId={0}", subscriberId);

                entry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }
        }

        private void CheckEchos()
        {
            _logger.Debug("Starting EchoTask");

            List<IStreamSubscriber> invalidConsumers = new List<IStreamSubscriber>();

            // acquiring the consumer here prevents to put another lock on the
            // dictionary
            IStreamSubscriber sendEchoConsumer = null;

            while(!_cancellationTokenSource.IsCancellationRequested)
            {
                foreach(var consumer in _consumers)
                {
                    if(sendEchoConsumer == null)
                        sendEchoConsumer = consumer.Value.Subscriber;

                    int tmp = consumer.Value.EchosCountDown;
                    consumer.Value.EchosCountDown--;

                    if(tmp != UDAPI.Configuration.MissedEchos)
                    {
                        _logger.WarnFormat("consumerId={0} missed count={1} echos", consumer.Key, UDAPI.Configuration.MissedEchos - tmp);

                        if (tmp <= 1)
                        {
                            _logger.WarnFormat("consumerId={0} missed count={1} echos and it will be disconnected", consumer.Key, UDAPI.Configuration.MissedEchos);
                            invalidConsumers.Add(consumer.Value.Subscriber);

                            if (sendEchoConsumer == consumer.Value.Subscriber)
                                sendEchoConsumer = null;
                        }
                    }
                }


                // this wil force indirectly a call to EchoManager.RemoveConsumer(consumer)
                // for the invalid consumers
                RemoveSubribers(invalidConsumers);

                invalidConsumers.Clear();

                SendEchos(sendEchoConsumer);

                _cancellationTokenSource.Token.WaitHandle.WaitOne(UDAPI.Configuration.EchoWaitInterval);
            }

            _logger.Debug("EchoTask terminated");
        }
      
        private static void RemoveSubribers(IEnumerable<IStreamSubscriber> subscribers)
        {
            foreach(var s in subscribers)
            {
                s.StopConsuming();
            }
        }

        private void SendEchos(IStreamSubscriber item)
        {
            if (item == null)
                return;

            try
            {
                item.Consumer.SendEcho();
            }
            catch (Exception e)
            {
                _logger.Error("Error sending echo-request", e);
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
