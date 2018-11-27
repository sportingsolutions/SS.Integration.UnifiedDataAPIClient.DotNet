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
using Akka.TestKit.NUnit;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests.MockedObjects
{
    internal class MockedStreamSubscriber : TestKit, IStreamSubscriber
    {
        public MockedStreamSubscriber(IConsumer consumer, IActorRef dispatcher)
        {
            Consumer = consumer;
            Dispatcher = dispatcher;
			
        }

	    public string _queue { get; set; }

	    #region IStreamSubscriber Members

        public IConsumer Consumer { get; set; }

        public IActorRef Dispatcher { get; set; }

        public void StopConsuming()
        {
            Dispatcher.Tell(new RemoveSubscriberMessage() { Subscriber = this });

        }

		//todo

	    public void NewFixture(IConsumer consumer)
	    {
		    throw new System.NotImplementedException();
	    }

	    //public void NewFixture(string queueName)
     //   {
     //       Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });
     //   }

        #endregion
    }
}
