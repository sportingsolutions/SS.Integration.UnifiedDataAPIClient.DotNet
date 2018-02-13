using System;
using Akka.Actor;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;
using SportingSolutions.Udapi.Sdk.Tests.MockedObjects.Actors;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    public class ScenarioTest : SdkTestKit
    {
        private const string Id1 = "Id1";
        private const string Id2 = "Id2";

        private readonly QueueDetails _queueDetails = new QueueDetails
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
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
        }

        [Test]
        public void HandleBasicConsumeTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var model = new Mock<IModel>();

            var echoControllerActor = ActorOfAsTestActorRef(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subscriber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);
            subscriber.HandleBasicConsumeOk("Connect");

            AwaitAssert(() =>
                {
                    echoControllerActor.UnderlyingActor.ConsumerCount.Should().Be(1);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void StopConsumingTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var model = new Mock<IModel>();

            var echoControllerActor = ActorOfAsTestActorRef(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subscriber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);
            subscriber.HandleBasicConsumeOk(Id1);

            AwaitAssert(() =>
                {
                    echoControllerActor.UnderlyingActor.ConsumerCount.Should().Be(1);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            subscriber.StopConsuming();

            AwaitAssert(() =>
                {
                    echoControllerActor.UnderlyingActor.ConsumerCount.Should().Be(0);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(0);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void DisconnectWithoutReconnectTest()
        {
            ((Configuration)UDAPI.Configuration).AutoReconnect = false;

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var echoControllerActor = ActorOfAsTestActorRef(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.DISCONNECTED);

            //register new consumer
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.CONNECTED);

                    echoControllerActor.UnderlyingActor.ConsumerCount.Should().Be(1);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));


            streamCtrlActorTestRef.UnderlyingActor.OnConnectionShutdown(null, new ShutdownEventArgs(ShutdownInitiator.Application, 541, "TestException"));

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor.ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void DisconnectWithReconnectTest()
        {
            ((Configuration)UDAPI.Configuration).AutoReconnect = true;
            ((Configuration)UDAPI.Configuration).DisconnectionDelay = 5;

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var echoControllerActor = ActorOfAsTestActorRef<EchoControllerActor>(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState
                        .DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //register new consumer
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState
                        .CONNECTED);

                    echoControllerActor.UnderlyingActor.ConsumerCount.Should().Be(1);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(1);
                },
                TimeSpan.FromMilliseconds(5 * ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(5 * ASSERT_EXEC_INTERVAL));

            streamCtrlActorTestRef.UnderlyingActor.OnConnectionShutdown(null,
                        new ShutdownEventArgs(ShutdownInitiator.Application, 541, "TestException"));

            streamCtrlActorTestRef.UnderlyingActor.StreamConnectionMock.Setup(c => c.IsOpen).Returns(true);

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                            .ConnectionState
                            .CONNECTED);
                },
                TimeSpan.FromMilliseconds(5 * ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(5 * ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void ConnectionShutdownCausesConsumersToStopStreamingTest()
        {
            ((Configuration)UDAPI.Configuration).AutoReconnect = true;
            ((Configuration)UDAPI.Configuration).DisconnectionDelay = 5;

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns(Id1);
            consumer1.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns(Id2);
            consumer2.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var echoControllerActor = ActorOfAsTestActorRef(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            AwaitAssert(() =>
                {

                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //register new consumer 1
            var newConsumerMessage = new NewConsumerMessage { Consumer = consumer1.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);
            //register new consumer 2
            newConsumerMessage = new NewConsumerMessage { Consumer = consumer2.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.CONNECTED);

                    echoControllerActor.UnderlyingActor.ConsumerCount.Should().Be(2);
                    updateDispatcherActor.UnderlyingActor.SubscribersCount.Should().Be(2);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            streamCtrlActorTestRef.UnderlyingActor.OnConnectionShutdown(null,
                        new ShutdownEventArgs(ShutdownInitiator.Application, 541, "TestException"));

            AwaitAssert(() =>
                {
                    consumer1.Verify(x => x.OnStreamDisconnected(), Times.Once,
                        "Consumer 1 was not disconnected on connection shutdown");
                    consumer2.Verify(x => x.OnStreamDisconnected(), Times.Once,
                        "Consumer 2 was not disconnected on connection shutdown");
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }
    }
}
