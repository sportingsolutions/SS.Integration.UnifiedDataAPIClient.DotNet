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
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    /// <summary>
    ///     Simple StreamController that mocks out the AMPQ 
    ///     stream controller
    /// </summary>
    internal class MockedStreamController : StreamController
    {
        private MockedStreamController(IDispatcher dispatcher)
            : base(dispatcher)
        {
        }

        public static void Register(IDispatcher dispatcher)
        {
            

            Instance = new MockedStreamController(dispatcher);
        }


        // expose this property
        public new StreamSubscriber Consumer
        {
            get { return base.Consumer; }
            set { base.Consumer = value; }
        }

        public IEchoController EchoManager
        {
            get
            {
                if (Dispatcher is UpdateDispatcher)
                    return ((UpdateDispatcher)Dispatcher).EchoManager;

                return null;
            }
        }

        protected override void EstablishConnection(ConnectionFactory factory)
        {
            if(Consumer != null)
                throw new Exception("Connection is already open");

            Consumer = new StreamSubscriber(Dispatcher);
            Consumer.SubscriberShutdown += OnModelShutdown;    
            OnConnectionStatusChanged(ConnectionState.CONNECTED);
        }

        protected override void AddConsumerToQueue(IConsumer consumer, string queueName)
        {
            if(Dispatcher.HasConsumer(consumer))
                throw new InvalidOperationException(string.Format("consumerId={0} already exists - cannot add it twice", consumer.Id));

            Dispatcher.AddConsumer(consumer);
        }

        protected override void RemoveConsumerFromQueue(IConsumer consumer, bool checkIfExists)
        {
            if(checkIfExists && !Dispatcher.HasConsumer(consumer))
                throw new InvalidOperationException(string.Format("consumerId={0} doesn't exist in the queue", consumer.Id));
        }
    }
}
