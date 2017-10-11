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
using Akka.Actor;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using IEchoController = SportingSolutions.Udapi.Sdk.Interfaces.SingleQueue.IEchoController;

namespace SportingSolutions.Udapi.Sdk.Actors.SingleQueue
{
    internal class EchoControllerActor : ReceiveActor, IEchoController
    {
        #region Constants

        public const string ActorName = "EchoControllerActor";

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(EchoControllerActor));
        private readonly EchoEntry _echoEntry;
        private readonly ICancelable _echoCancellation = new Cancelable(Context.System.Scheduler);

        #endregion

        #region Properties

        public bool Enabled { get; }

        /// <summary>
        /// Only one consumer is needed for checking queue is alive as there is only one queue for all the consumers
        /// </summary>
        internal int ConsumerCount => _echoEntry?.Consumer != null ? 1 : 0;

        internal int EchosCountDown => _echoEntry.EchosCountDown;

        #endregion

        #region Constructors

        public EchoControllerActor()
        {
            Enabled = UDAPI.Configuration.UseEchos;
            _echoEntry = new EchoEntry { Consumer = null, EchosCountDown = UDAPI.Configuration.MissedEchos };

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
            Receive<AllConsumersDisconnectedMessage>(x => RemoveConsumer());
            Receive<EchoMessage>(x => ProcessEcho());
            Receive<SendEchoMessage>(x => CheckEchos());
            Receive<ResetEchoesMessage>(x => ResetEchoes());
            Receive<DisposeMessage>(x => Dispose());
        }

        #endregion

        #region Implementation of IEchoController

        public virtual void AddConsumer(IConsumer consumer)
        {
            if (!Enabled || _echoEntry.Consumer != null || consumer == null)
                return;

            _echoEntry.Consumer = consumer;
            _echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;

            _logger.DebugFormat("consumerId={0} was added to echos manager", consumer.Id);
        }

        public void RemoveConsumer()
        {
            if (!Enabled)
                return;

            if (_echoEntry.Consumer != null)
                _logger.DebugFormat("consumerId={0} was removed from echos manager", _echoEntry.Consumer.Id);

            _echoEntry.Consumer = null;
        }

        public void ProcessEcho()
        {
            _echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;
        }

        public void ResetEchoes()
        {
            //this is used on disconnection when auto-reconnection is used
            //some echoes will usually be missed in the process 
            //once the connection is restarted it makes sense to reset echo counter
            _echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            _logger.DebugFormat("Disposing EchoSender");
            _echoCancellation.Cancel();

            RemoveConsumer();

            _logger.InfoFormat("EchoSender correctly disposed");
        }

        #endregion

        #region Private methods

        private void CheckEchos()
        {
            if (_echoEntry.Consumer == null)
            {
                _logger.DebugFormat("Consumer is not set - echo will not be sent");
                return;
            }

            try
            {
                try
                {
                    int tmp = _echoEntry.EchosCountDown;
                    _echoEntry.EchosCountDown--;

                    if (tmp != UDAPI.Configuration.MissedEchos)
                    {
                        _logger.WarnFormat("missed count={0} echos", UDAPI.Configuration.MissedEchos - tmp);

                        if (tmp <= 1)
                        {
                            _logger.WarnFormat("missed count={0} echos - removing consumer",
                                UDAPI.Configuration.MissedEchos);
                            Context.ActorSelection(SdkActorSystem.StreamControllerActorPath)
                                .Tell(new RemoveConsumerMessage { Consumer = _echoEntry.Consumer });
                            RemoveConsumer();
                        }
                    }

                    SendEchos();
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

        private void SendEchos()
        {
            if (_echoEntry.Consumer == null)
            {
                _logger.Warn("Unable to send echo due to null consumer object reference");
                return;
            }
            try
            {
                _logger.DebugFormat("Sending batch echo");
                _echoEntry.Consumer.SendEcho();
            }
            catch (Exception e)
            {
                _logger.Error("Error sending echo-request", e);
            }

        }

        #endregion

        #region Private messages

        private class SendEchoMessage
        {
        }

        #endregion

        #region Private types

        private class EchoEntry
        {
            public IConsumer Consumer;
            public int EchosCountDown;
        }

        #endregion
    }
}
