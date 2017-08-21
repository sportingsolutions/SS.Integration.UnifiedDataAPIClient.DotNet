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

using Akka.Actor;
using Akka.IO;
using log4net;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class ResourceActor : ReceiveActor
    {
        protected ILog Logger;
        private IConsumer _resource;


        public ResourceActor(IConsumer resource)
            //: base(restItem, client)
        {
            _resource = resource;
            
            Logger = LogManager.GetLogger(typeof(ResourceActor));
            Logger.DebugFormat("Instantiated fixtureName=\"{0}\" fixtureId=\"{1}\"", resource, Id);

            Become(DisconnectedState);

            //Receive<ConnectMessage>(connectMsg => Connected(connectMsg));
            //Receive<StreamUpdateMessage>(streamMsg => StreamUpdate(streamMsg));
            //Receive<DisconnectMessage>(msg => Disconnect(msg));
        }

        private void DisconnectedState()
        {
            Receive<ConnectMessage>(connectMsg => Connected(connectMsg));
            Receive<DisconnectMessage>(msg => Disconnect(msg));
        }

        private void Disconnect(DisconnectMessage msg)
        {
            Logger.DebugFormat($"Disconnection message raised for {msg.Id}");
            _resource.OnStreamDisconnected();
        }

        public static Props Props(IConsumer resource)
        {
            return Akka.Actor.Props.Create<ResourceActor>(() => new ResourceActor(resource));
        }

        private void StreamUpdate(StreamUpdateMessage streamMsg)
        {
            Logger.DebugFormat($"New update arrived for {streamMsg.Id}");
            _resource.OnStreamEvent(new StreamEventArgs(streamMsg.Message));
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

        

        public void StartStreaming()
        {
            //StartStreaming(DEFAULT_ECHO_INTERVAL_MS, DEFAULT_ECHO_MAX_DELAY_MS);
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new NewConsumerMessage() { Consumer = _resource });
            //StreamController.Instance.AddConsumer(_resource, echoInterval, echoMaxDelay);
            Logger.DebugFormat("REQUESTED Streaming request queued for fixtureName=\"{0}\" fixtureId=\"{1}\"", ((IResource) _resource).Name, Id);
        }

        public void PauseStreaming()
        {
            //Logger.DebugFormat("Streaming paused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            //_pauseStream.Reset();
        }

        public void UnPauseStreaming()
        {
            //Logger.DebugFormat("Streaming unpaused for fixtureName=\"{0}\" fixtureId={1}", Name, Id);
            //_pauseStream.Set();
        }

        public void StopStreaming()
        {
            SdkActorSystem.ActorSystem.ActorSelection(SdkActorSystem.StreamControllerActorPath).Tell(new RemoveConsumerMessage() { Consumer = _resource });

            //StreamController.Instance.RemoveConsumer(_resource);
            Logger.DebugFormat("REQUESTED Streaming stopped for fixtureName=\"{0}\" fixtureId=\"{1}\"", ((IResource)_resource).Name, Id);
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

