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
using System.Threading;
using Akka.Actor;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using SportingSolutions.Udapi.Sdk.Tests.MockedObjects.Actors;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    internal class StreamTestsActor : SdkTestKit
    {
        private MockedStreamControllerActor _streamControllerAct;

        private readonly QueueDetails _queryDetails = new QueueDetails
        {
            Host = "testhost",
            Name = "testname",
            Password = "testpassword",
            Port = 5672,
            UserName = "testuser",
            VirtualHost = "vhost"
        };

        [SetUp]
        public void Initialise()
        {
            SdkActorSystem.InitializeActors = false;
            SdkActorSystem.ActorSystem = Sys;
            ((Configuration)UDAPI.Configuration).UseEchos = false;
        }

        [Test]
        public void EstablishConnectionTest()
        {
            // STEP 1: prepare mocked data


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("testing");
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queryDetails);

            var updateDispatcherActor = ActorOf(() => new UpdateDispatcherActor());

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            // is the controller in its initial state?
            streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.DISCONNECTED);

            //register new consumer
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object, CallerName = "StreamTestsActor.EstablishConnectionTest" };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            //ExpectMsg<ConnectStreamMessage>();

            //StreamController.Instance.AddConsumer(consumer.Object, -1, -1);

            streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.CONNECTED);

            streamCtrlActorTestRef.Tell(new RemoveConsumerMessage { Consumer = consumer.Object });

            streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.CONNECTED);
        }

        [Test]
        public void HandleFailedConnectionAttemptTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("testing");
            var e = new Exception("Cannot find queue details");
            consumer.Setup(x => x.GetQueueDetails()).Throws(e);

            var updateDispatcherActor = ActorOf(() => new UpdateDispatcherActor());

            // for this test, we need the real StreamController, not the mocked one
            var streamCtrlActorTestRef = ActorOfAsTestActorRef<StreamControllerActor>(
                Props.Create(() => new StreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.DISCONNECTED);

            streamCtrlActorTestRef.Tell(new NewConsumerMessage() { Consumer = consumer.Object, CallerName = "StreamTestsActor.HandleFailedConnectionAttemptTest" });

            streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.DISCONNECTED);
            streamCtrlActorTestRef.UnderlyingActor.ConnectionError.Should().Be(e);

        }

        [Test]
        public void RemoveConsumerTest()
        {
            // STEP 1: prepare mocked data


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("testing");
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queryDetails);
            consumer.Setup(x => x.OnStreamConnected());

            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(() => new MockedStreamControllerActor(updateDispatcherActor), StreamControllerActor.ActorName);

            AwaitAssert(() =>
                {
                    // is the controller in its initial state?
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(0);

            // STEP 2: add a consumer      
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object, CallerName = "StreamTestsActor.RemoveConsumerTest" };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            AwaitAssert(() =>
                {
                    // STEP 3: check that up to now, everythin is ok
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.CONNECTED);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            AwaitAssert(() =>
                {
                    var streamSubscriberObj =
                        updateDispatcherActor.Ask<IStreamSubscriber>(new RetrieveSubscriberMessage { Id = "testing" }).Result;
                    streamSubscriberObj.Should().NotBeNull();
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            // STEP 4: remove the consumer
            streamCtrlActorTestRef.Tell(new RemoveConsumerMessage { Consumer = consumer.Object });

            AwaitAssert(() =>
                {
                    // STEP 5: check the outcome
                    consumer.Verify(x => x.OnStreamDisconnected(), Times.Once,
                            "Consumer was not disconnect on connection shutdonw");

                    updateDispatcherActor.Ask(new SubscribersCountMessage()).Result.Should().Be(0);

                    var subscriberResult = updateDispatcherActor.Ask(new RetrieveSubscriberMessage { Id = "testing" },
                        TimeSpan.FromSeconds(9));

                    subscriberResult.Result.Should().BeOfType<NotFoundMessage>();

                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.CONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void DisposeTest()
        {
            // is the controller in its initial state?
            var updateDispatcherActor = ActorOf<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);
            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(() => new MockedStreamControllerActor(updateDispatcherActor), StreamControllerActor.ActorName);

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            Mock<IConsumer>[] consumers = new Mock<IConsumer>[100];

            // STEP 1: prepare mocked data

            for (int i = 0; i < 100; i++)
            {

                Mock<IConsumer> consumer = new Mock<IConsumer>();
                consumer.Setup(x => x.Id).Returns("testing_" + i);
                consumer.Setup(x => x.GetQueueDetails()).Returns(_queryDetails);
                consumers[i] = consumer;

                // STEP 2: add the consumers
                streamCtrlActorTestRef.Tell(new NewConsumerMessage { Consumer = consumer.Object, CallerName = "StreatTestsActor.DisposeTest" });

                //StreamController.Instance.AddConsumer(consumer.Object, -1, -1);
            }

            AwaitAssert(() =>
                {
                    // STEP 2: check if the connection was correctly established
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                            .ConnectionState.CONNECTED);

                    Thread.Yield();

                    for (int i = 0; i < 100; i++)
                        consumers[i].Verify(x => x.OnStreamConnected(), Times.Once, "Connection event was not raised");
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            // STEP 4
            streamCtrlActorTestRef.Tell(new DisposeMessage());

            AwaitAssert(() =>
                {
                    Thread.Yield();

                    for (int i = 0; i < 100; i++)
                    {
                        consumers[i].Verify(x => x.OnStreamConnected(), Times.Once, "Connection event was not raised");
                        consumers[i].Verify(x => x.OnStreamDisconnected(), Times.Once,
                            "Connection event was not raised");
                    }

                    updateDispatcherActor.Ask(new SubscribersCountMessage()).Result.Should().Be(0);
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
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
            ThreadPool.SetMinThreads(500, 500);

            var updateDispatcherActor = ActorOf<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(() => new MockedStreamControllerActor(updateDispatcherActor), StreamControllerActor.ActorName);

            // STEP 1: prepare mocked data

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            object _lock = new object();

            int testIterations = 10;

            Mock<IConsumer>[] consumers = new Mock<IConsumer>[testIterations];

            for (int i = 0; i < testIterations; i++)
            {
                Mock<IConsumer> consumer = new Mock<IConsumer>();
                consumer.Setup(x => x.Id).Returns("testing_" + i);
                consumer.Setup(x => x.GetQueueDetails()).Returns(_queryDetails);
                consumers[i] = consumer;

                // when the stream connected event is raised, just wait...
                // note that the event is raised async
                // this call will block OnStreamConnected and prevent it from going any further
                // Monitor.Wait is there to stop the method from exiting
                consumer.Setup(x => x.OnStreamConnected()).Callback(() =>
                {
                    lock (_lock)
                    {
                        Monitor.Wait(_lock);
                    }

                });

                streamCtrlActorTestRef.Tell(new NewConsumerMessage { Consumer = consumer.Object, CallerName = "StreatTestsActor.IgnoreUpdatesOnDisconnectionTest" });
            }

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                            .ConnectionState.CONNECTED);
                    updateDispatcherActor.Ask<int>(new SubscribersCountMessage()).Result.Should().Be(testIterations);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
            // send some messages
            for (int i = 0; i < testIterations; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    updateDispatcherActor.Tell(
                        new StreamUpdateMessage() { Id = "testing_" + i, Message = "UPDATE_" + j });
                }
            }

            updateDispatcherActor.Tell(new RemoveAllSubscribers());

            AwaitAssert(() =>
                {
                    for (int i = 0; i < testIterations; i++)
                    {
                        consumers[i].Verify(x => x.OnStreamEvent(It.IsAny<StreamEventArgs>()), Times.Never,
                            "Updates shouldn't have been processed");
                    }

                    // this releases the lock and allows all threads to complete 
                    lock (_lock)
                    {
                        Monitor.PulseAll(_lock);
                    }
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void ProcessPendingUpdatesTest()
        {
            int counter = 0;
            bool multipleThreadsIn = false;


            var updateDispatcherAct =
                ActorOf<UpdateDispatcherActor>(() => new UpdateDispatcherActor());

            var consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("ABC");
            consumer.Setup(x => x.OnStreamEvent(It.IsAny<StreamEventArgs>())).Callback(() =>
            {

                if (!Monitor.TryEnter(this))
                    multipleThreadsIn = true;
                else
                {
                    counter++;
                    Thread.Sleep(100);
                    Monitor.Exit(this);
                }

            });

            var subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);

            updateDispatcherAct.Tell(new NewSubscriberMessage { Subscriber = subscriber.Object });

            for (int i = 0; i < 100; i++)
            {
                updateDispatcherAct.Tell(new StreamUpdateMessage() { Id = "ABC", Message = "message" });
            }

            AwaitAssert(() =>
                {
                    multipleThreadsIn.Should().BeFalse();
                    counter.Should().Be(100);
                },
                TimeSpan.FromMilliseconds(6 * ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(6 * ASSERT_EXEC_INTERVAL));
        }
    }
}
