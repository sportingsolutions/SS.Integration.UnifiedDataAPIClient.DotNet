using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.NUnit;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    public class EchoControllerActorTest : TestKit
    {

        //private EchoControllerActor testing;

        private const string id1 = "Id1";
        private const string id2 = "Id2";
        private const string id3 = "Id3";

        [SetUp]
        public void Initialise()
        {
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
            SdkActorSystem.Init(Sys, false);


        }

        private EchoControllerActor GetEchoControllerActorWith1Consumer(string consumerId)
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            testing.AddConsumer(subsctiber.Object);

            return testing;
        }



        private void AddConsumer(EchoControllerActor actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.AddConsumer(subsctiber.Object);

        }

        private TestActorRef<MockedEchoControllerActor> GetMockedEchoControllerActorWith1Consumer(string consumerId)
        {
            var mock = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);

            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            mock.UnderlyingActor.AddConsumer(subsctiber.Object);

            return mock;
        }

        private void AddConsumer(TestActorRef<MockedEchoControllerActor> actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.UnderlyingActor.AddConsumer(subsctiber.Object);

        }


        [Test]
        public void AddConsumerWithNullTest()
        {
            var testing = new EchoControllerActor();
            testing.AddConsumer(null);
            testing.ConsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void Add1ConsumerTest()
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            testing.AddConsumer(subsctiber.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);


        }

        [Test]
        public void AddSameConsumerTest()
        {
            var testing = GetEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id1);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);
        }

        [Test]
        public void RemoveConsumerPositiveTest()
        {
            var testing = new EchoControllerActor();


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);



            testing.AddConsumer(subsctiber.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);

            testing.RemoveConsumer(subsctiber.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void RemoveConsumerNegativeTest()
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns(id1);

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns(id2);


            Mock<IStreamSubscriber> subsctiber1 = new Mock<IStreamSubscriber>();
            subsctiber1.Setup(x => x.Consumer).Returns(consumer1.Object);

            Mock<IStreamSubscriber> subsctiber2 = new Mock<IStreamSubscriber>();
            subsctiber2.Setup(x => x.Consumer).Returns(consumer2.Object);


            testing.AddConsumer(subsctiber1.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);
            testing.RemoveConsumer(subsctiber2.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);

        }

        [Test]
        public void GetDefaultEchosCountDownTest()
        {
            var testing = new EchoControllerActor();


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            testing.AddConsumer(subsctiber.Object);
            testing.GetEchosCountDown(id1).ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos);
        }

        [Test]
        public void CheckEchosDecteaseEchosCountDownTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            var message = new EchoControllerActor.SendEchoMessage();

            testing.Tell(message);
            testing.UnderlyingActor.GetEchosCountDown(id1).ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos - 1);

        }

        [Test]
        public void CheckEchosUntillAllSubscribersClearTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id2);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);

            var message = new EchoControllerActor.SendEchoMessage();
            for (int i = 0; i < UDAPI.Configuration.MissedEchos; i++)
            {
                testing.Tell(message);
            }

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void CheckEchosWithProcessEchoClearTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id2);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage() { Id = id2 };

            testing.Tell(sendEchoMessage);
            testing.Tell(echoMessage);
            testing.Tell(sendEchoMessage);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(2);
            testing.UnderlyingActor.GetEchosCountDown(id2).ShouldBeEquivalentTo(1);
        }


        [Test]
        public void SendEchoCallTest()
        {
            var testing = ActorOfAsTestActorRef(() => new MockedEchoControllerActor(), EchoControllerActor.ActorName);

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);
            int sendEchoCallCount = 0;
            consumer.Setup(x => x.SendEcho()).Callback(() => sendEchoCallCount++);

            Mock<IStreamSubscriber> subsctiber1 = new Mock<IStreamSubscriber>();
            subsctiber1.Setup(x => x.Consumer).Returns(consumer.Object);

            testing.UnderlyingActor.AddConsumer(subsctiber1.Object);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage() { Id = id1 };

            testing.Tell(sendEchoMessage);
            sendEchoCallCount.Should().Be(1);
            testing.UnderlyingActor
                .GetEchosCountDown(consumer.Object.Id)
                .Should()
                .Be(UDAPI.Configuration.MissedEchos - 1);
            testing.Tell(echoMessage);
            testing.UnderlyingActor
                .GetEchosCountDown(consumer.Object.Id)
                .Should()
                .Be(UDAPI.Configuration.MissedEchos);
        }
    }
}
