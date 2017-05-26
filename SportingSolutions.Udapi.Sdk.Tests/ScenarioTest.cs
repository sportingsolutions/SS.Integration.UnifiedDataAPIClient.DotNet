using System.Threading;
using Akka.Actor;
using Akka.TestKit.NUnit;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    public class ScenarioTest : TestKit
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
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
            SdkActorSystem.Init(Sys, false);
        }

        [Test]
        public void HandleBasicConsumeTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var model = new Mock<IModel>();

            var echoController = new EchoController();
            var echoControllerActor = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subsctiber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);
            subsctiber.HandleBasicConsumeOk("Connect");

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            updateDispatcherActor.UnderlyingActor.SubscribersCount.ShouldBeEquivalentTo(1);
        }

        [Test]
        public void StopConsumingTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var model = new Mock<IModel>();

            var echoControllerActor = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subsctiber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);
            subsctiber.HandleBasicConsumeOk("Connect");

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            updateDispatcherActor.UnderlyingActor.SubscribersCount.ShouldBeEquivalentTo(1);

            subsctiber.StopConsuming();

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(0);
            updateDispatcherActor.UnderlyingActor.SubscribersCount.ShouldBeEquivalentTo(0);
        }

        [Test]
        public void DisconnectWithoutReconnectTest()
        {
            ((Configuration)UDAPI.Configuration).AutoReconnect = false;


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var model = new Mock<IModel>();

            var echoControllerActor = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subsctiber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.DISCONNECTED);

            //register new consumer
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.CONNECTED);

            //subsctiber.HandleBasicConsumeOk("Connect");

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            updateDispatcherActor.UnderlyingActor.SubscribersCount.ShouldBeEquivalentTo(1);

            streamCtrlActorTestRef.UnderlyingActor.OnConnectionShutdown(null, new ShutdownEventArgs(ShutdownInitiator.Application, 541, "TestException"));
            //_streamConnection.Abort(541, "Test exception");

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.DISCONNECTED);
        }

        [Test]
        public void DisconnectWithReconnectTest()
        {
            ((Configuration)UDAPI.Configuration).AutoReconnect = true;
            ((Configuration)UDAPI.Configuration).DisconnectionDelay = 5;

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

            var echoControllerActor = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.DISCONNECTED);

            //register new consumer
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.CONNECTED);

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            updateDispatcherActor.UnderlyingActor.SubscribersCount.ShouldBeEquivalentTo(1);

            streamCtrlActorTestRef.UnderlyingActor.OnConnectionShutdown(null, new ShutdownEventArgs(ShutdownInitiator.Application, 541, "TestException"));

            Thread.Sleep(500);

            streamCtrlActorTestRef.UnderlyingActor.StreamConnectionMock.Setup(c => c.IsOpen).Returns(true);

            Thread.Sleep((((Configuration)UDAPI.Configuration).DisconnectionDelay + 1) * 1000);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.CONNECTED);
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

            var echoControllerActor = ActorOfAsTestActorRef(() => new MockedEchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.DISCONNECTED);

            //register new consumer 1
            var newConsumerMessage = new NewConsumerMessage { Consumer = consumer1.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);
            //register new consumer 2
            newConsumerMessage = new NewConsumerMessage { Consumer = consumer2.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.CONNECTED);

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);
            updateDispatcherActor.UnderlyingActor.SubscribersCount.ShouldBeEquivalentTo(2);

            streamCtrlActorTestRef.UnderlyingActor.OnConnectionShutdown(null, new ShutdownEventArgs(ShutdownInitiator.Application, 541, "TestException"));

            Thread.Sleep((((Configuration)UDAPI.Configuration).DisconnectionDelay + 1) * 1000);

            consumer1.Verify(x => x.OnStreamDisconnected(), Times.Once, "Consumer 1 was not disconnect on connection shutdown");
            consumer2.Verify(x => x.OnStreamDisconnected(), Times.Once, "Consumer 2 was not disconnect on connection shutdown");
            streamCtrlActorTestRef.UnderlyingActor.State.ShouldBeEquivalentTo(StreamControllerActor.ConnectionState.DISCONNECTED);
        }
    }
}
