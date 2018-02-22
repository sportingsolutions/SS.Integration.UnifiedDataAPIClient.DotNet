//Copyright 2018 Spin Services Limited

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
using System.Threading;
using Akka.Actor;
using Moq;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests.MockedObjects.Actors
{
    /// <summary>
    /// Simple StreamController that mocks out the AMPQ stream controller
    /// </summary>
    internal class MockedStreamControllerActor : StreamControllerActor
    {
        private bool _connected = false;

        public MockedStreamControllerActor(IActorRef dispatcher)
            : base(dispatcher)
        {
            Instance = this;
        }

        public static StreamControllerActor Instance { get; set; }

        public Mock<IConnection> StreamConnectionMock { get; set; } = new Mock<IConnection>();

        public void ForceCloseConnection()
        {
            base.CloseConnection();
        }

        protected override void EstablishConnection(ConnectionFactory factory)
        {
            TestLogger.Instance.WriteLine($"In MockedStreamControllerActor.EstablishConnection: Before ConnectionStatusChanged event state is {this.State}");
            var newstate = ConnectionState.DISCONNECTED;
            try
            {
                newstate = ConnectionState.CONNECTED;
            }
            finally
            {
                OnConnectionStatusChanged(ConnectionState.CONNECTED);
            }
        
            _streamConnection = StreamConnectionMock.Object;

            TestLogger.Instance.WriteLine($"In MockedStreamControllerActor.EstablishConnection: After ConnectionStatusChanged event state is {this.State}");
        }

        protected override void AddConsumerToQueue(IConsumer consumer)
        {
            new MockedStreamSubscriber(consumer, Dispatcher).StartConsuming("");
        }

        protected override void RemoveConsumerFromQueue(IConsumer consumer)
        {
            var s = Dispatcher.Ask(new RetrieveSubscriberMessage {Id = consumer.Id}).Result as IStreamSubscriber;

            if (s == null)
                throw new Exception("Subscriber with Id=" + consumer.Id + " not found");

            s.StopConsuming();
        }

        public override void Dispose()
        {
            base.Dispose();
            TestLogger.Instance.WriteLine($"In MockedStreamControllerActor.Dispose: StreamControllerActor deaded");
        }
        
        /*
        protected override void ConnectedState()
        {
            base.ConnectedState();
            TestLogger.Instance.WriteLine($"In MockedStreamControllerActor.ConnectedState: state is {this.State}");
            _connected = true;
        }

        protected override void DisconnectedState()
        {
            base.DisconnectedState();
            TestLogger.Instance.WriteLine($"In MockedStreamControllerActor.DisconnectedState: state is {this.State}");
            _connected = false;
        }
        */
    }
}
