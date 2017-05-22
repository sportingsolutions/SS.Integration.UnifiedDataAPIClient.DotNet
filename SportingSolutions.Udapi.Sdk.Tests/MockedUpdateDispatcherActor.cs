//Copyright 2017 Spin Services Limited

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
using System.Threading.Tasks;
using Akka.Actor;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    /// <summary>
    ///     Mock for UpdateDispatcherActor that mocks out the ResourceActor
    /// </summary>
    internal class MockedUpdateDispatcherActor : UpdateDispatcherActor
    {
        private readonly IActorRef _resourceActor;

        public MockedUpdateDispatcherActor(IActorRef resourceActor)
        {
            _resourceActor = resourceActor;
            Become(MockedUpdateDispatcherBehavior);
        }

        private void MockedUpdateDispatcherBehavior()
        {
            ReceiveAsync<NewConsumerMessage>(NewConsumerMessageHandler);
            ReceiveAsync<DisposeMessage>(DisposeMessageHandler);
            ReceiveAsync<DisconnectMessage>(DisconnectMessageHandler);
            ReceiveAsync<StreamUpdateMessage>(StreamUpdatetMessageHandler);
        }

        private async Task NewConsumerMessageHandler(NewConsumerMessage message)
        {
            await Task.Run(() =>
            {
                _resourceActor.Tell(new ConnectMessage() {Id = message.Consumer.Id, Consumer = message.Consumer});
            }).ConfigureAwait(false);
        }

        private async Task DisposeMessageHandler(DisposeMessage message)
        {
            await Task.Run(() =>
            {
                _resourceActor.Tell(new DisconnectMessage());
            }).ConfigureAwait(false);
        }

        private async Task DisconnectMessageHandler(DisconnectMessage message)
        {
            await Task.Run(() =>
            {
                _resourceActor.Tell(message);
            }).ConfigureAwait(false);
        }

        private async Task StreamUpdatetMessageHandler(StreamUpdateMessage message)
        {
            await Task.Run(() =>
            {
                _resourceActor.Tell(message);
            }).ConfigureAwait(false);
        }
    }
}
