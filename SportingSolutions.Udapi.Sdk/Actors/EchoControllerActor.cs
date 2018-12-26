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
using System.Runtime.Remoting.Contexts;
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
            public IStreamSubscriber Subscriber;
            public int  EchosCountDown;
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

            Receive<NewSubscriberMessage>(x => AddConsumer(x.Subscriber));
            Receive<RemoveSubscriberMessage>(x => RemoveConsumer(x.Subscriber, x.MessageId));
            Receive<EchoMessage>(x => ProcessEcho(x.Id, x.MessageId));
            Receive<SendEchoMessage>(x => CheckEchos());
            Receive<DisposeMessage>(x => Dispose());
        }



        protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));

            StopConsumingAll();
            Context.ActorSelection(SdkActorSystem.FaultControllerActorPath).Tell(new CriticalActorRestartedMessage() { ActorName = ActorName });

            base.PreRestart(reason, message);
        }

        private void StopConsumingAll()
        {
            if (_consumers != null)
                RemoveSubribers(_consumers.Values.Select(_ => _.Subscriber));
        }

        public bool Enabled { get; private set; }

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

        public void RemoveConsumer(IStreamSubscriber subscriber, System.Guid messageId)
        {
            if (!Enabled || subscriber == null)
            {
                _logger.DebugFormat("consumerId={0} didn't remove from echos manager, useEchos={1}, messageId={2}", subscriber?.Consumer?.Id, Enabled, messageId);
                return;
            }

            EchoEntry tmp;
            if (_consumers.TryRemove(subscriber.Consumer.Id, out tmp))
                _logger.DebugFormat("consumerId={0} removed from echos manager, messageId={1}", subscriber.Consumer.Id, messageId);
            else
                _logger.DebugFormat("consumerId={0} has already removed, messageId={1}", subscriber.Consumer.Id,messageId);
        }

        public void RemoveAll()
        {
            if (!Enabled)
                return;

            _consumers.Clear();
        }

        public void ProcessEcho(string subscriberId, System.Guid messageId)
        {
            EchoEntry entry;
            if (!string.IsNullOrEmpty(subscriberId) && _consumers.TryGetValue(subscriberId, out entry))
            {
                if (UDAPI.Configuration.VerboseLogging)
                    _logger.DebugFormat("Resetting echo information for fixtureId={0}, messageId={1}", subscriberId, messageId);

                entry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            }
        }

        private void CheckEchos()
        {
            try
            {
                List<IStreamSubscriber> invalidConsumers = new List<IStreamSubscriber>();

                // acquiring the consumer here prevents to put another lock on the
                // dictionary
                
                _logger.Info($"CheckEchos consumersCount={_consumers.Count}");

	            IStreamSubscriber sendEchoConsumer = _consumers.Values.FirstOrDefault(_ => _.EchosCountDown > 0)?.Subscriber;

				foreach (var consumer in _consumers)
                {
                    if (consumer.Value.EchosCountDown < UDAPI.Configuration.MissedEchos)
                    {
                        var msg = $"consumerId={consumer.Key} missed count={UDAPI.Configuration.MissedEchos - consumer.Value.EchosCountDown} echos";
                        if (consumer.Value.EchosCountDown < 1)
                        {
                            _logger.Warn($"{msg} and it will be disconnected");
                            invalidConsumers.Add(consumer.Value.Subscriber);
                        }
                        else
                        {
                            _logger.Info(msg);
                        }
                    }
                    consumer.Value.EchosCountDown--;
                }
				
				// this wil force indirectly a call to EchoManager.RemoveConsumer(consumer)
				// for the invalid consumers
				RemoveSubribers(invalidConsumers);

                invalidConsumers.Clear();

                SendEchos(sendEchoConsumer);
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
                Context.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new RemoveConsumerMessage() { Consumer = s.Consumer });
                Self.Tell(new RemoveSubscriberMessage() { Subscriber = s, MessageId = Guid.NewGuid()});
            }
        }

        private void SendEchos(IStreamSubscriber item)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                _logger.DebugFormat("Sending batch echo");
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
            _echoCancellation.Cancel();
            
            RemoveAll();
            
            _logger.InfoFormat("EchoSender correctly disposed");
        }

        #region Private messages

        internal class SendEchoMessage
        {
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

        #endregion
    }
}
