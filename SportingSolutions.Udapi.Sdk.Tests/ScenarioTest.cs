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
            //ToDo unactual due to reconnect logic change
            /*
            ((Configuration)UDAPI.Configuration).AutoReconnect = false;

            var echoControllerActor = ActorOfAsTestActorRef(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);

           
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
                */
        }

        [Test]
        public void DisconnectWithReconnectTest()
        {
            
            ((Configuration)UDAPI.Configuration).AutoReconnect = true;
            ((Configuration)UDAPI.Configuration).DisconnectionDelay = 10;

            var echoControllerActor = ActorOfAsTestActorRef<EchoControllerActor>(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName);

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);
            
            consumer.Setup(x => x.OnStreamDisconnected()).Callback(() =>
            {
                //streamCtrlActorTestRef.UnderlyingActor.ForceCloseConnection();
            });

  

            AwaitAssert(() =>
                {
                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState
                        .DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //register new consumer
            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            var newConsumerMessage = new NewConsumerMessage() { Consumer = consumer.Object };
            streamCtrlActorTestRef.UnderlyingActor.StreamConnectionMock.Setup(c => c.IsOpen).Returns(true);
            streamCtrlActorTestRef.Tell(newConsumerMessage);

            echoControllerActor.UnderlyingActor.AddConsumer(subscriber.Object);
            updateDispatcherActor.Tell(new NewSubscriberMessage() { Subscriber = subscriber.Object });

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
            ((Configuration)UDAPI.Configuration).AutoReconnect = false;
            ((Configuration)UDAPI.Configuration).DisconnectionDelay = 5;
            var echoControllerActor = ActorOfAsTestActorRef(() => new EchoControllerActor(), EchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);
            var streamCtrlActorTestRef = ActorOfAsTestActorRef<MockedStreamControllerActor>(
                Props.Create(() => new MockedStreamControllerActor(updateDispatcherActor)),
                StreamControllerActor.ActorName); 

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            
            consumer1.Setup(x => x.Id).Returns(Id1);
            consumer1.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);
            consumer1.Setup(x => x.OnStreamDisconnected()).Callback(() =>
            {
                streamCtrlActorTestRef.UnderlyingActor.ForceCloseConnection();
            });


            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns(Id2);
            consumer2.Setup(x => x.GetQueueDetails()).Returns(_queueDetails);
            consumer2.Setup(x => x.OnStreamDisconnected()).Callback(() => { });
            

            
            
            AwaitAssert(() =>
                {

                    streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        .ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //register new consumer 1
            streamCtrlActorTestRef.UnderlyingActor.StreamConnectionMock.Setup(c => c.IsOpen).Returns(true);
            var newConsumerMessage = new NewConsumerMessage { Consumer = consumer1.Object };
            streamCtrlActorTestRef.Tell(newConsumerMessage);
            
            //register new consumer 2
            newConsumerMessage = new NewConsumerMessage { Consumer = consumer2.Object };

            streamCtrlActorTestRef.Tell(newConsumerMessage);
            Mock<IStreamSubscriber> subscriber1 = new Mock<IStreamSubscriber>();
            subscriber1.Setup(x => x.Consumer).Returns(consumer1.Object);
            Mock<IStreamSubscriber> subscriber2 = new Mock<IStreamSubscriber>();
            subscriber2.Setup(x => x.Consumer).Returns(consumer2.Object);

            echoControllerActor.UnderlyingActor.AddConsumer(subscriber1.Object);
            echoControllerActor.UnderlyingActor.AddConsumer(subscriber2.Object);
            updateDispatcherActor.Tell(new NewSubscriberMessage() {Subscriber = subscriber1.Object});
            updateDispatcherActor.Tell(new NewSubscriberMessage() {Subscriber = subscriber2.Object});

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
            ((Configuration)UDAPI.Configuration).AutoReconnect = true;
            AwaitAssert(() =>
                {
                    consumer1.Verify(x => x.OnStreamDisconnected(), Times.Once,
                        "Consumer 1 was not disconnected on connection shutdown");
                    consumer2.Verify(x => x.OnStreamDisconnected(), Times.Once,
                        "Consumer 2 was not disconnected on connection shutdown");
                        //streamCtrlActorTestRef.UnderlyingActor.State.Should().Be(StreamControllerActor
                        //ConnectionState.DISCONNECTED);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }
    }
}
