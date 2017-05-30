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
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.Impl;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    /// <summary>
    ///     Simple StreamController that mocks out the AMPQ 
    ///     stream controller
    /// </summary>
    internal class MockedStreamControllerActor : StreamControllerActor
    {
        private IActorRef _echoActorRef;

        public MockedStreamControllerActor(IActorRef dispatcherActorRef, IActorRef echoActorRef)
            : base(dispatcherActorRef)
        {
            Instance = this;
            _echoActorRef = echoActorRef;
        }

        public static StreamControllerActor Instance { get; set; }

        public Mock<IConnection> StreamConnectionMock { get; set; } = new Mock<IConnection>();

        public void ForceCloseConnection()
        {
            base.CloseConnection();
        }
        
        protected override void EstablishConnection(ConnectionFactory factory)
        {
            OnConnectionStatusChanged(ConnectionState.CONNECTED);
            _streamConnection = StreamConnectionMock.Object;
        }

        protected override void AddConsumerToQueue(IConsumer consumer)
        {
            new MockedStreamSubscriber(consumer, Dispatcher).StartConsuming("");
            _echoActorRef.Tell(new NewConsumerMessage { Consumer = consumer });
        }
    }
}
