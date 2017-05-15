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

        [SetUp]
        public void Initialise()
        {
            ((Configuration)UDAPI.Configuration).UseEchos = true;
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

            //testing = new EchoControllerActor();

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            var testing = mock.As<EchoControllerActor>();

            testing.AddConsumer(subsctiber.Object);

            return mock;
        }

        private void AddConsumer(TestActorRef<MockedEchoControllerActor> actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.As<EchoControllerActor>().AddConsumer(subsctiber.Object);

        }


        [Test]
        public void AddConsumerWithNullTest()
        {
            var testing = new EchoControllerActor();
            testing.AddConsumer(null);
            testing.CoinsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void Add1ConsumerTest()
        {
            var testing = new EchoControllerActor();
            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("Id1");


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subsctiber.Object);
            testing.CoinsumerCount.ShouldBeEquivalentTo(1);
            

        }

        [Test]
        public void AddSameConsumerTest()
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns("Id1");

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns("Id1");


            Mock<IStreamSubscriber> subsctiber1 = new Mock<IStreamSubscriber>();
            subsctiber1.Setup(x => x.Consumer).Returns(consumer1.Object);

            Mock<IStreamSubscriber> subsctiber2 = new Mock<IStreamSubscriber>();
            subsctiber2.Setup(x => x.Consumer).Returns(consumer2.Object);


            testing.AddConsumer(subsctiber1.Object);
            testing.AddConsumer(subsctiber2.Object);
            testing.CoinsumerCount.ShouldBeEquivalentTo(1);

        }

        [Test]
        public void RemoveConsumerPositiveTest()
        {
            var testing = new EchoControllerActor();

            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("Id1");


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);



            testing.AddConsumer(subsctiber.Object);
            testing.CoinsumerCount.ShouldBeEquivalentTo(1);

            testing.RemoveConsumer(subsctiber.Object);
            testing.CoinsumerCount.ShouldBeEquivalentTo(0);

        }

        [Test]
        public void RemoveConsumerNegativeTest()
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns("Id1");

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns("Id2");


            Mock<IStreamSubscriber> subsctiber1 = new Mock<IStreamSubscriber>();
            subsctiber1.Setup(x => x.Consumer).Returns(consumer1.Object);

            Mock<IStreamSubscriber> subsctiber2 = new Mock<IStreamSubscriber>();
            subsctiber2.Setup(x => x.Consumer).Returns(consumer2.Object);


            testing.AddConsumer(subsctiber1.Object);
            testing.CoinsumerCount.ShouldBeEquivalentTo(1);
            testing.RemoveConsumer(subsctiber2.Object);
            testing.CoinsumerCount.ShouldBeEquivalentTo(1);

        }

        [Test]
        public void GetDefaultEchosCountDownTest()
        {
            var testing = new EchoControllerActor();


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns("Id1");


            Mock<IStreamSubscriber> subsctiber = new Mock<IStreamSubscriber>();
            subsctiber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subsctiber.Object);
            testing.GetEchosCountDown("Id1").ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos);
        }

        [Test]
        public void CheckEchosDecteaseEchosCountDownTest()
        {
            var id1 = "Id1";
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            var message = new EchoControllerActor.SendEchoMessage();

            testing.Tell(message);
            testing.As<EchoControllerActor>().GetEchosCountDown(id1).ShouldBeEquivalentTo(UDAPI.Configuration.MissedEchos-1);

        }

        [Test]
        public void CheckEchosUntillAllSubscribersClearTest()
        {
            var id1 = "Id1";
            var id2 = "Id2";
            var testing = GetMockedEchoControllerActorWith1Consumer(id1);
            AddConsumer(testing, id2);

            testing.As<EchoControllerActor>().CoinsumerCount.ShouldBeEquivalentTo(2);

            var message = new EchoControllerActor.SendEchoMessage();
            for (int i = 0; i < UDAPI.Configuration.MissedEchos; i++)
            {
                testing.Tell(message);
            }

            testing.As<EchoControllerActor>().CoinsumerCount.ShouldBeEquivalentTo(0);

        }



        //[Test]
        //public void GetDefaultEchosCountDownTest()
        //{
        //}


    }
}
