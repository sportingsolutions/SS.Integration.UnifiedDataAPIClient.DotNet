using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit.NUnit;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    public class ScenarioTest : TestKit
    {
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

        [Test]
        public void HandleBasicConsumeTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(Querydetails);

            var model = new Mock<IModel>();

            var echoController = new EchoController();
            var echoControllerActor = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subsctiber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);
            subsctiber.HandleBasicConsumeOk("Connect");

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            updateDispatcherActor.UnderlyingActor.SubscribersCout.ShouldBeEquivalentTo(1);

            



        }

        [Test]
        public void StopConsumingTest()
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(id1);
            consumer.Setup(x => x.GetQueueDetails()).Returns(Querydetails);

            var model = new Mock<IModel>();

            var echoControllerActor = ActorOfAsTestActorRef<MockedEchoControllerActor>(() => new MockedEchoControllerActor(), MockedEchoControllerActor.ActorName);
            var updateDispatcherActor = ActorOfAsTestActorRef<UpdateDispatcherActor>(() => new UpdateDispatcherActor(), UpdateDispatcherActor.ActorName);

            var subsctiber = new StreamSubscriber(model.Object, consumer.Object, updateDispatcherActor);
            subsctiber.HandleBasicConsumeOk("Connect");

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(1);
            updateDispatcherActor.UnderlyingActor.SubscribersCout.ShouldBeEquivalentTo(1);

            subsctiber.StopConsuming();

            echoControllerActor.UnderlyingActor.ConsumerCount.ShouldBeEquivalentTo(0);
            updateDispatcherActor.UnderlyingActor.SubscribersCout.ShouldBeEquivalentTo(0);



        }




        private QueueDetails Querydetails = new QueueDetails
            {
                Host = "testhost",
                Name = "testname",
                Password = "testpassword",
                Port = 5672,
                UserName = "testuser",
                VirtualHost = "vhost"
            };



}
}
