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
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class ResourceActor : ReceiveActor
    {
        protected ILog Logger;
        private readonly IConsumer _resource;
        private string id;

        public ResourceActor(IConsumer resource)
            //: base(restItem, client)
        {
            id = this.GetHashCode().ToString();
            _resource = resource;
            
            Logger = LogManager.GetLogger(typeof(ResourceActor).ToString());
            Logger.Debug($"resourceActorId={id} Instantiated fixtureName=\"{resource}\" fixtureId=\"{Id}\"");

            Become(DisconnectedState);
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Logger.Error(
                $"resourceActorId={id} Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        private void DisconnectedState()
        {
            Receive<ConnectMessage>(connectMsg => Connected(connectMsg));
            Receive<DisconnectMessage>(msg => Disconnect(msg));
        }

        private void Disconnect(DisconnectMessage msg)
        {
            Logger.Debug($"resourceActorId={id} Disconnection message raised for {msg.Id}");
            _resource.OnStreamDisconnected();
        }

        private void StreamUpdate(StreamUpdateMessage streamMsg)
        {
            Logger.Debug($"resourceActorId={id} New update arrived for {streamMsg.Id}");
            _resource.OnStreamEvent(new StreamEventArgs(streamMsg.Message, streamMsg.ReceivedAt));
        }

        private void Connected(ConnectMessage connectMsg)
        {
            _resource.OnStreamConnected();
            Become(BecomeConnected);
        }

        private void BecomeConnected()
        {
            Receive<StreamUpdateMessage>(streamMsg => StreamUpdate(streamMsg));
            Receive<DisconnectMessage>(msg => Disconnect(msg));
		}

	    

	    #region IResource Members

        public string Id
        {
            get { return _resource.Id; }
        }
        
        public bool IsDisposed { get; internal set; }

        

        
        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            Logger.Debug($"resourceActorId={id} REQUESTING Streaming request for fixtureName=\"{((IResource)_resource)?.Name}\" fixtureId=\"{Id}\"");
            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new NewConsumerMessage() { Consumer = _resource });
            Logger.Debug($"resourceActorId={id} REQUESTED Streaming request queued for fixtureName=\"{((IResource)_resource)?.Name}\" fixtureId=\"{Id}\"");
        }

        public void PauseStreaming()
        {
            //Logger.Debug("Streaming paused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            //_pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            //Logger.Debug("Streaming unpaused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            //_pauseStream.Set();
        }

		
	    //TODO review
		public void StopStreaming()
		{
			SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.UpdateDispatcherPath).Tell(new RemoveConsumerMessage() {Consumer = _resource});

			//StreamController.Instance.RemoveConsumer(_resource);
			Logger.Debug($"resourceActorId={id} REQUESTED Streaming stopped for fixtureName=\"{((IResource)_resource)?.Name}\" fixtureId=\"{Id}\"");
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
        {
            StopStreaming();
            IsDisposed = true;
        }

        #endregion

        #region IConsumer Members

        }

    #endregion
    }

