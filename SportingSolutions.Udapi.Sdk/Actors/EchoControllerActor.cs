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
using System.Linq;
using Akka.Actor;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    internal class EchoControllerActor : ReceiveActor, IEchoController
    {
        private class EchoEntry
        {
            public IConsumer Consumer;
            public int EchosCountDown;
        }

        internal class SendEchoMessage
        {
        }

        public const string ActorName = "EchoControllerActor";

        private readonly ILog _logger = LogManager.GetLogger(typeof(EchoControllerActor));

        private readonly ConcurrentDictionary<string, EchoEntry> _consumers;
        private readonly ICancelable _echoCancellation = new Cancelable(Context.System.Scheduler);

        public EchoControllerActor()
        {
            Enabled = UDAPI.Configuration.UseEchos;
            _consumers = new ConcurrentDictionary<string, EchoEntry>();

            if (Enabled)
            {
                //this will send Echo Message to the EchoControllerActor (Self) at the specified interval
                Context.System.Scheduler.ScheduleTellRepeatedly(
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromMilliseconds(UDAPI.Configuration.EchoWaitInterval),
                    Self,
                    new SendEchoMessage(),
                    ActorRefs.Nobody);
            }

            _logger.DebugFormat("EchoSender is {0}", Enabled ? "enabled" : "disabled");

            Receive<NewConsumerMessage>(x => AddConsumer(x.Consumer));
            Receive<RemoveConsumerMessage>(x => RemoveConsumer(x.Consumer));
            Receive<RemoveAllConsumersMessage>(x => RemoveAll());
            Receive<EchoMessage>(x => ProcessEcho(x.Id));
            Receive<SendEchoMessage>(x => CheckEchos());
            Receive<ResetAllEchoesMessage>(x => ResetAll());
            Receive<DisposeMessage>(x => Dispose());
        }

        public bool Enabled { get; }

        public virtual void AddConsumer(IConsumer consumer)
        {
            if (!Enabled || consumer == null)
                return;

            _consumers[consumer.Id] = new EchoEntry
            {
                Consumer = consumer,
                EchosCountDown = UDAPI.Configuration.MissedEchos
            };

            _logger.DebugFormat("consumerId={0} added to echos manager", consumer.Id);
        }

        public void ResetAll()
        {
            //this is used on disconnection when auto-reconnection is used
            //some echoes will usually be missed in the process 
            //once the connection is restarted it makes sense to reset echo counter
            foreach (var echoEntry in _consumers.Values)
            {
                echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }
        }

        public void RemoveConsumer(IConsumer consumer)
        {
            if (!Enabled || consumer == null)
                return;

            EchoEntry tmp;
            if (_consumers.TryRemove(consumer.Id, out tmp))
                _logger.DebugFormat("consumerId={0} removed from echos manager", consumer.Id);
        }

        public void RemoveAll()
        {
            if (!Enabled)
                return;

            _consumers.Clear();
        }

        public void ProcessEcho(string consumerId)
        {
            EchoEntry echoEntry;
            if (_consumers.TryGetValue(consumerId, out echoEntry))
            {
                echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }
            else
            {
                _logger.WarnFormat(
                    "Failed process echo for consumer with id {0} as it could not be found...",
                    consumerId);
            }
        }

        private void CheckEchos()
        {
            if (_consumers.IsEmpty)
            {
                _logger.DebugFormat("There are no subscribers - echo will not be sent");
            }

            try
            {
                List<IConsumer> invalidConsumers = new List<IConsumer>();

                // acquiring the consumer here prevents to put another lock on the
                // dictionary
                IConsumer sendEchoConsumer = null;

                try
                {
                    foreach (var consumer in _consumers)
                    {
                        if (sendEchoConsumer == null)
                            sendEchoConsumer = consumer.Value.Consumer;

                        int tmp = consumer.Value.EchosCountDown;
                        consumer.Value.EchosCountDown--;

                        if (tmp != UDAPI.Configuration.MissedEchos)
                        {
                            _logger.WarnFormat("consumerId={0} missed count={1} echos", consumer.Key,
                                UDAPI.Configuration.MissedEchos - tmp);

                            if (tmp <= 1)
                            {
                                _logger.WarnFormat("consumerId={0} missed count={1} echos and it will be disconnected",
                                    consumer.Key, UDAPI.Configuration.MissedEchos);
                                invalidConsumers.Add(consumer.Value.Consumer);

                                if (sendEchoConsumer == consumer.Value.Consumer)
                                    sendEchoConsumer = null;
                            }
                        }
                    }

                    // this wil force indirectly a call to EchoManager.RemoveConsumer(consumer)
                    // for the invalid consumers
                    RemoveConsumers(invalidConsumers);

                    invalidConsumers.Clear();

                    SendEchos(sendEchoConsumer);
                }
                catch (Exception ex)
                {
                    _logger.Error("Check Echos loop has experienced a failure", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Check Echos has experienced a failure", ex);
            }
        }

        private void RemoveConsumers(IEnumerable<IConsumer> consumers)
        {
            foreach (var consumer in consumers)
            {
                Context.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                    .Tell(new RemoveConsumerMessage {Consumer = consumer});
                Self.Tell(new RemoveConsumerMessage {Consumer = consumer});
            }
        }

        private void SendEchos(IConsumer item)
        {
            if (item == null)
            {
                _logger.Warn("Unable to send echo due to null consumer object reference");
                return;
            }
            try
            {
                _logger.DebugFormat("Sending batch echo");
                item.SendEcho();
            }
            catch (Exception e)
            {
                _logger.Error("Error sending echo-request", e);
            }

        }

        public void Dispose()
        {
            _logger.DebugFormat("Disposing EchoSender");
            _echoCancellation.Cancel();

            RemoveAll();

            _logger.InfoFormat("EchoSender correctly disposed");
        }

        internal int? GetEchosCountDown(string subscriberId)
        {
            EchoEntry entry;
            if (!string.IsNullOrEmpty(subscriberId) && _consumers.TryGetValue(subscriberId, out entry))
            {
                return entry.EchosCountDown;
            }
            return null;
        }

        internal int ConsumerCount => _consumers.Count;
    }
}
