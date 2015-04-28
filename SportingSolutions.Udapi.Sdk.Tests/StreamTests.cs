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
using RabbitMQ.Client;
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
            MockedStreamController.Register(new UpdateDispatcher());
        }

        [Test]
        public void EstablishConnectionTest()
        {
            // STEP 1: prepare mocked data
            QueueDetails details = new QueueDetails
            {
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
        public void CloseConnectionTest()
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


            ((MockedStreamController)StreamController.Instance).Consumer.Should().NotBeNull();

            ((MockedStreamController)StreamController.Instance).Consumer.HandleModelShutdown(null, new ShutdownEventArgs(ShutdownInitiator.Peer, 0, "Testing"));

            Thread.Sleep(1000);
            Thread.Yield();

            consumer.Verify(x => x.OnStreamDisconnected(), Times.Once, "Consumer was not disconnect on connection shutdonw");

            StreamController.Instance.Dispatcher.Should().NotBeNull();
            StreamController.Instance.Dispatcher.ConsumersCount.Should().Be(0);
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED);
            ((MockedStreamController)StreamController.Instance).Consumer.Should().BeNull();
        }

        [Test]
        public void DisposeTest()
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

            StreamController.Instance.Dispose();

            Thread.Sleep(1000);
            Thread.Yield();

            consumer.Verify(x => x.OnStreamDisconnected(), Times.Once, "Consumer was not disconnected when the connection was disposed");
            StreamController.Instance.Dispatcher.ConsumersCount.Should().Be(0);
            ((MockedStreamController)StreamController.Instance).Consumer.Should().BeNull();
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
                    VirtualHost = "vhost_" + i
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
            StreamController.Instance.Dispatcher.ConsumersCount.Should().Be(10000);

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
