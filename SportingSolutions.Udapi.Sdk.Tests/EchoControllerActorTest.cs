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

            testing.AddConsumer(consumer.Object);

            return testing;
        }



        private void AddConsumer(EchoControllerActor actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);

            actor.AddConsumer(consumer.Object);

        }

        private TestActorRef<MockedEchoControllerActor> GetMockedEchoControllerActorWith1Consumer(string consumerId)
        {
            var mock = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);

            mock.UnderlyingActor.AddConsumer(consumer.Object);

            return mock;
        }

        private void AddConsumer(TestActorRef<MockedEchoControllerActor> actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);

            actor.UnderlyingActor.AddConsumer(consumer.Object);

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

            testing.AddConsumer(consumer.Object);
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

            testing.AddConsumer(consumer.Object);
            testing.ConsumerCount.ShouldBeEquivalentTo(1);

            testing.RemoveConsumer();
            testing.ConsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void GetDefaultEchosCountDownTest()
        {
            var testing = new EchoControllerActor();


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);

            testing.AddConsumer(consumer.Object);
            testing.GetEchosCountDown().ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos);
        }

        [Test]
        public void CheckEchosDecteaseEchosCountDownTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            var message = new EchoControllerActor.SendEchoMessage();

            testing.Tell(message);
            testing.UnderlyingActor.GetEchosCountDown().ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos - 1);

        }

        [Test]
        public void CheckEchosUntillAllSubscribersClearTest()
        {
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id2);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);

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

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage();

            testing.Tell(sendEchoMessage);
            testing.Tell(echoMessage);
            testing.Tell(sendEchoMessage);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            testing.UnderlyingActor.GetEchosCountDown().ShouldBeEquivalentTo(2);
        }


        [Test]
        public void SendEchoCallTest()
        {
            var testing = ActorOfAsTestActorRef(() => new MockedEchoControllerActor(), EchoControllerActor.ActorName);

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);
            int sendEchoCallCount = 0;
            consumer.Setup(x => x.SendEcho()).Callback(() => sendEchoCallCount++);

            testing.UnderlyingActor.AddConsumer(consumer.Object);

            testing.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage { Id = id1 };

            testing.Tell(sendEchoMessage);
            sendEchoCallCount.Should().BeGreaterThan(0);
            testing.UnderlyingActor
                .GetEchosCountDown()
                .Should()
                .Be(UDAPI.Configuration.MissedEchos - 1);
            testing.Tell(echoMessage);
            testing.UnderlyingActor
                .GetEchosCountDown()
                .Should()
                .Be(UDAPI.Configuration.MissedEchos);
        }
    }
}
