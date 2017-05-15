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
        internal class EchoEntry
        {
            public IStreamSubscriber Subscriber;
            public int EchosCountDown;
        }

        public const string ActorName = "EchoControllerActor";

        private readonly ILog _logger = LogManager.GetLogger(typeof(EchoControllerActor));


        private readonly ConcurrentDictionary<string, EchoEntry> _consumers;
        public int CoinsumerCount => _consumers.Count;

        ICancelable _echoCancellation = new Cancelable(Context.System.Scheduler);

        public EchoControllerActor()
        {
            Enabled = UDAPI.Configuration.UseEchos;
            _consumers = new ConcurrentDictionary<string, EchoEntry>();

            if (Enabled)
            {
                //this will send Echo Message to the EchoControllerActor (Self) at the specified interval
                Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0), TimeSpan.FromMilliseconds(UDAPI.Configuration.EchoWaitInterval), Self, GetEchoMessage(), ActorRefs.Nobody);
            }

            _logger.DebugFormat("EchoSender is {0}", Enabled ? "enabled" : "disabled");

            Receive<NewSubscriberMessage>(x => AddConsumer(x.Subscriber));
            Receive<RemoveSubscriberMessage>(x => RemoveConsumer(x.Subscriber));
            Receive<EchoMessage>(x => ProcessEcho(x.Id));
            Receive<SendEchoMessage>(x => CheckEchos());
            Receive<DisposeMessage>(x => Dispose());

        }
        

        private SendEchoMessage GetEchoMessage()
        {
            if (_consumers.IsEmpty)
            {
                _logger.WarnFormat("Can't send echo - there are no subscribers");
                return new SendEchoMessage {Subscriber = null};
            }

            //this should only return message to send
            return new SendEchoMessage() { Subscriber = _consumers.First().Value.Subscriber };
        }

        public bool Enabled { get; internal set; }

        public virtual void AddConsumer(IStreamSubscriber subscriber)
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
            if (!Enabled)
                return;

            _consumers.Clear();
        }

        public void ProcessEcho(string subscriberId)
        {
            EchoEntry entry;
            if (!string.IsNullOrEmpty(subscriberId) && _consumers.TryGetValue(subscriberId, out entry))
            {
                if (UDAPI.Configuration.VerboseLogging)
                    _logger.DebugFormat("Resetting echo information for consumerId={0}", subscriberId);

                entry.EchosCountDown = UDAPI.Configuration.MissedEchos;
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
                List<IStreamSubscriber> invalidConsumers = new List<IStreamSubscriber>();

                // acquiring the consumer here prevents to put another lock on the
                // dictionary
                IStreamSubscriber sendEchoConsumer = null;

                //while (!_cancellationTokenSource.IsCancellationRequested)
                //{
                try
                {
                    foreach (var consumer in _consumers)
                    {
                        if (sendEchoConsumer == null)
                            sendEchoConsumer = consumer.Value.Subscriber;

                        int tmp = consumer.Value.EchosCountDown;
                        consumer.Value.EchosCountDown--;

                        if (tmp != UDAPI.Configuration.MissedEchos)
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

                    //      _cancellationTokenSource.Token.WaitHandle.WaitOne(UDAPI.Configuration.EchoWaitInterval);
                }
                catch (Exception ex)
                {
                    _logger.Error("Check Echos loop has experienced a failure", ex);
                }
                // }

                //_logger.Debug("EchoTask terminated");
            }
            catch (Exception ex)
            {
                _logger.Error("Check Echos has experienced a failure", ex);
            }
        }

        private void RemoveSubribers(IEnumerable<IStreamSubscriber> subscribers)
        {
            foreach (var s in subscribers)
            {
                s.StopConsuming();
                Self.Tell(new RemoveSubscriberMessage() { Subscriber = s });
            }

            foreach(var s in subscribers)
            {
                Context.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new RemoveConsumerMessage() { Consumer = s.Consumer });
                Self.Tell(new RemoveSubscriberMessage() { Subscriber = s });
            }
        }

        private void SendEchos(IStreamSubscriber item)
        {

            if (item == null)
            {
                _logger.Warn("Unable to send echo due to null stream subscriber");
                return;
            }
            try
            {
                _logger.DebugFormat("Sending batch echoe");
                item.Consumer.SendEcho();
            }
            catch (Exception e)
            {
                _logger.Error("Error sending echo-request", e);
            }

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

        public void Dispose()
        {
            _logger.DebugFormat("Disposing EchoSender");
            _echoCancellation.Cancel();
            
            RemoveAll();
            
            _logger.InfoFormat("EchoSender correctly disposed");
        }

        #region Private messages

        internal class SendEchoMessage
        {
            internal IStreamSubscriber Subscriber { get; set; }
        }

        #endregion
    }
}
