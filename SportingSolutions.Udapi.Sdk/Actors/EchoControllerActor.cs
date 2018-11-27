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
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
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
	    private string _virtualHost;
	    private string _echoLink;





		private EchoEntry _echoEntry;
        private readonly ICancelable _echoCancellation = new Cancelable(Context.System.Scheduler);

        public EchoControllerActor()
        {
            Enabled = UDAPI.Configuration.UseEchos;
            

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
            Receive<RemoveSubscriberMessage>(x => RemoveConsumer(x.Subscriber));
            Receive<EchoMessage>(x => ProcessEcho(x.Id));
            Receive<SendEchoMessage>(x => CheckEchos());
            Receive<DisposeMessage>(x => Dispose());
	        Receive<EchoLinksMessage>(x => SaveEchoLinks(x));
			

		}

	    private void SaveEchoLinks(EchoLinksMessage echoLinksMessage)
	    {
		    _virtualHost = echoLinksMessage.VirtualHost;
			_echoLink = echoLinksMessage.EchoLink;
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
           _echoEntry = null;
        }

        public bool Enabled { get; private set; }

        public virtual void AddConsumer(IStreamSubscriber subscriber)
        {
            if (!Enabled || subscriber == null)
                return;

            _echoEntry = new EchoEntry
            {
                Subscriber = subscriber,
                EchosCountDown = UDAPI.Configuration.MissedEchos
            };

            _logger.DebugFormat("consumerId={0} added to echos manager", subscriber);
        }

        public void ResetAll()
        {
			//this is used on disconnection when auto-reconnection is used
			//some echoes will usually be missed in the process 
			//once the connection is restarted it makes sense to reset echo counter
			if (_echoEntry != null)
				_echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;

        }

        public void RemoveConsumer(IStreamSubscriber subscriber)
        {
            if (!Enabled || subscriber == null)
                return;

            EchoEntry tmp;
	        if (_echoEntry != null)
	        {
		        _logger.DebugFormat("consumerId={0} removed from echos manager", _echoEntry.Subscriber);
		        _echoEntry = null;
	        }
        }

        public void RemoveAll()
        {
            if (!Enabled)
                return;

            _echoEntry = null;
        }

        public void ProcessEcho(string subscriberId)
        {
            if (UDAPI.Configuration.VerboseLogging)
                    _logger.DebugFormat("Resetting echo information for fixtureId={0}", subscriberId);

	        _echoEntry.EchosCountDown = UDAPI.Configuration.MissedEchos;
            
        }

        private void CheckEchos()
        {
			if (_echoEntry == null)
				return;

	        try
            {
                _logger.Info($"CheckEchos called");
                {
                   
                    if (_echoEntry.EchosCountDown < UDAPI.Configuration.MissedEchos)
                    {
                        var msg = $"consumerId={_echoEntry} missed count={UDAPI.Configuration.MissedEchos - _echoEntry.EchosCountDown} echos";
                        if (_echoEntry.EchosCountDown < 1)
                        {
                            _logger.Warn($"{msg} and it will be disconnected");
                            SubriberErrored();
                        }
                        else
                        {
                            _logger.Info(msg);
                        }
                    }
                    _echoEntry.EchosCountDown--;
                }

	            SendEchos(_echoEntry.Subscriber);

			}
            catch (Exception ex)
            {
                _logger.Error("Check Echos has experienced a failure", ex);
            }
        }

        private void SubriberErrored()
        {
	        //todo resume
			Context.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new SubriberErroredMessage() );
			//Self.Tell(new RemoveSubscriberMessage() { Subscriber = _echoEntry?.Subscriber});
			
		}

        private void SendEchos(IStreamSubscriber item)
        {
            try
            {
                _logger.DebugFormat("Sending batch echo");
	            SendEcho();

            }
            catch (Exception e)
            {
                _logger.Error("Error sending echo-request", e);
            }

        }

	    public void SendEcho()
	    {
			if (SessionFactory.ConnectClient == null || _virtualHost == null || _echoLink == null)
				return;

		    _logger.Debug($"Sending echo to {_echoLink} {_virtualHost}");

			var streamEcho = new StreamEcho
		    {
			    Host = _virtualHost,
			    Message = Guid.NewGuid() + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
		    };

		    var echouri = new Uri(_echoLink);

			var response = SessionFactory.ConnectClient.Request(echouri, RestSharp.Method.POST, streamEcho, UDAPI.Configuration.ContentType, 3000);
		    if (response.ErrorException != null || response.Content == null)
		    {
			    RestErrorHelper.LogRestError(_logger, response, "Error sending echo request");
			    throw new Exception(string.Format("Error calling {0}", echouri), response.ErrorException);
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

        //internal int? GetEchosCountDown(string subscriberId)
        //{
        //    EchoEntry entry;
        //    if (!string.IsNullOrEmpty(subscriberId) && _echoEntry.TryGetValue(subscriberId, out entry))
        //    {
        //        return entry.EchosCountDown;
        //    }
        //    return null;
        //}

        //internal int ConsumerCount => _echoEntry.Count;

        #endregion
    }
}
