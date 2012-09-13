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


using System.Threading;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    internal class StreamTests
    {
        [SetUp]
        public void Initialise()
        {
            ((Configuration)UDAPI.Configuration).UseEchos = false;
            MockedStreamController.Register(new UpdateDispatcher());
        }

        [Test]
        public void EstablishConnectionTest()
        {
            // STEP 1: prepare mocked data
            QueueDetails details = new QueueDetails {
                Host = "testhost",
                Name = "testname",
                Password = "testpassword",
                Port = 5672,
                UserName = "testuser",
                VirtualHost = "vhost"
            };

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("testing");
            consumer.Setup(x => x.GetQueueDetails()).Returns(details);

            // is the controller in its initial state?
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED);

            StreamController.Instance.AddConsumer(consumer.Object, -1, -1);

            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED);

            StreamController.Instance.RemoveConsumer(consumer.Object);

            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED);
        }

        [Test]
        public void RemoveConsumerTest()
        {
            // STEP 1: prepare mocked data

            QueueDetails details = new QueueDetails {
                Host = "testhost",
                Name = "testname",
                Password = "testpassword",
                Port = 5672,
                UserName = "testuser",
                VirtualHost = "vhost"
            };

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("testing");
            consumer.Setup(x => x.GetQueueDetails()).Returns(details);

            // is the controller in its initial state?
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED);

            // STEP 2: add a consumer
            StreamController.Instance.AddConsumer(consumer.Object, -1, -1);

            // STEP 3: check that up to now, everythin is ok
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED);
            StreamController.Instance.Dispatcher.GetSubscriber("testing").Should().NotBeNull();

            // STEP 4: remove the consumer
            StreamController.Instance.Dispatcher.RemoveSubscriber(StreamController.Instance.Dispatcher.GetSubscriber("testing"));

            Thread.Sleep(1000);
            Thread.Yield();

            // STEP 5: check the outcome
            consumer.Verify(x => x.OnStreamDisconnected(), Times.Once, "Consumer was not disconnect on connection shutdonw");

            StreamController.Instance.Dispatcher.Should().NotBeNull();
            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(0);
            StreamController.Instance.Dispatcher.GetSubscriber("testing").Should().BeNull();
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED);
        }

        [Test]
        public void DisposeTest()
        {
            // is the controller in its initial state?
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED);
            Mock<IConsumer>[] consumers = new  Mock<IConsumer>[10000];

            // STEP 1: prepare mocked data

            for (int i = 0; i < 10000; i++)
            {
                QueueDetails details = new QueueDetails {
                    Host = "testhost",
                    Name = "testname",
                    Password = "testpassword",
                    Port = 5672,
                    UserName = "testuser",
                    VirtualHost = "vhost"
                };

                Mock<IConsumer> consumer = new Mock<IConsumer>();
                consumer.Setup(x => x.Id).Returns("testing_" + i);
                consumer.Setup(x => x.GetQueueDetails()).Returns(details);
                consumers[i] = consumer;

                // STEP 2: add the consumers
                StreamController.Instance.AddConsumer(consumer.Object, -1, -1);
            }

           
            // STEP 2: check if the connection was correctly established
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED);

            Thread.Sleep(2000);
            Thread.Yield();

            for(int i = 0; i < 10000; i++)
                consumers[i].Verify(x => x.OnStreamConnected(), Times.Once, "Connection event was not raised");

            // STEP 4
            StreamController.Instance.Dispose();

            Thread.Sleep(2000);
            Thread.Yield();

            for (int i = 0; i < 10000; i++)
            {
                consumers[i].Verify(x => x.OnStreamConnected(), Times.Once, "Connection event was not raised");
                consumers[i].Verify(x => x.OnStreamDisconnected(), Times.Once, "Connection event was not raised");
            }

            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(0);
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED);
        }
    
        /// <summary>
        ///     In this test I want to make sure that 
        ///     if a consumer has pending updates, these
        ///     won't get processed if there is a disconnection.;
        /// 
        ///     In other words, I want to make sure that 
        ///     the disconnection event has the higher priority
        ///     wrt update messages
        /// 
        /// </summary>
        [Test]
        public void IgnoreUpdatesOnDisconnectionTest()
        {
            // STEP 1: prepare mocked data

            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED);

            object _lock = new object();

            Mock<IConsumer>[] consumers = new Mock<IConsumer>[10000];

            for (int i = 0; i < 10000; i++)
            {
                QueueDetails details = new QueueDetails {
                    Host = "testhost",
                    Name = "testname",
                    Password = "testpassword",
                    Port = 5672,
                    UserName = "testuser",
                    VirtualHost = "vhost"
                };
                
                Mock<IConsumer> consumer = new Mock<IConsumer>();
                consumer.Setup(x => x.Id).Returns("testing_" + i);
                consumer.Setup(x => x.GetQueueDetails()).Returns(details);
                consumers[i] = consumer;

                // when the stream connected event is raised, just wait...
                // note that the event is raised async
                consumer.Setup(x => x.OnStreamConnected()).Callback( () => {lock(_lock) { Monitor.Wait( _lock);} });
                StreamController.Instance.AddConsumer(consumer.Object, -1, -1);
            }

            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED);
            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(10000);

            // send some messages
            for(int i = 0; i < 10000; i++)
            {
                for(int j = 0; j < 3; j++)
                {
                    StreamController.Instance.Dispatcher.DispatchMessage("testing_" + i, "UPDATE_" + j);
                }
            }

            StreamController.Instance.Dispatcher.RemoveAll();
            lock(_lock)
            {
                Monitor.PulseAll(_lock);
            }

            Thread.Sleep(2000);


            for(int i = 0; i < 10000; i++)
            {
                consumers[i].Verify(x => x.OnStreamEvent(It.IsAny<StreamEventArgs>()), Times.Never, "Updates shouldn't have been processed");
            }
            
        }
    
    }
}
