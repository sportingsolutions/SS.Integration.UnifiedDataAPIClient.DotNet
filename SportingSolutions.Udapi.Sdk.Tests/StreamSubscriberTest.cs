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
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    class StreamSubscriberTest : TestKit
    {
        [SetUp]
        public void Initialise()
        {
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
            SdkActorSystem.Init(Sys, false);
        }


        [Test]
        public void StartConsumingTest()
        {
            var model = new Mock<IModel>();
            var consumer = new Mock<IConsumer>();
            var actor = new Mock<IActorRef>();

            var test = new StreamSubscriber(model.Object, consumer.Object, actor.Object);


            int startConsume = 0;
            model.Setup(x => x.BasicConsume("test", true, consumer.Object.Id, test)).Callback(() => startConsume++);
            test.StartConsuming("test");

           startConsume.ShouldBeEquivalentTo(1);

        }

        [Test]
        public void StopShouldNotCauseErrorTest()
        {
            var model = new Mock<IModel>();
            var consumer = new Mock<IConsumer>();
            var actor = new Mock<IActorRef>();

            var test = new StreamSubscriber(model.Object, consumer.Object, actor.Object);

            model.Setup(x => x.BasicConsume("test", true, consumer.Object.Id, test)).Callback(() => { });
            test.StartConsuming("test");

            test.StopConsuming();
            test.StopConsuming();



        }

        [Test]
        public void StopShouldSetDisposeFlatToFalseTest()
        {
            var model = new Mock<IModel>();
            var consumer = new Mock<IConsumer>();
            var actor = new Mock<IActorRef>();
            var test = new StreamSubscriber(model.Object, consumer.Object, actor.Object);
            model.Setup(x => x.BasicConsume("test", true, consumer.Object.Id, test)).Callback(() => { });
            test.IsDisposed.ShouldBeEquivalentTo(false);

            test.StartConsuming("test");
            test.StopConsuming();

            test.IsDisposed.ShouldBeEquivalentTo(true);
        }


        [Test]
        public void StopShouldsENDdIApatchesRemoveMessageTest()
        {
            var model = new Mock<IModel>();
            var consumer = new Mock<IConsumer>();
            var actor = new Mock<IActorRef>();
            var test = new StreamSubscriber(model.Object, consumer.Object, actor.Object);
            model.Setup(x => x.BasicConsume("test", true, consumer.Object.Id, test)).Callback(() => { });
            test.IsDisposed.ShouldBeEquivalentTo(false);

            test.StartConsuming("test");
            test.StopConsuming();

            //TODO check RemoveSubscriberMessage
        }

        [Test]
        public void DoubleStopShouldNotCauseErrorTest()
        {
            var model = new Mock<IModel>();
            var consumer = new Mock<IConsumer>();
            var actor = new Mock<IActorRef>();

            var test = new StreamSubscriber(model.Object, consumer.Object, actor.Object);

            model.Setup(x => x.BasicConsume("test", true, consumer.Object.Id, test)).Callback(() => { });
            test.StartConsuming("test");

            test.StopConsuming();
            test.StopConsuming();
        }

    }
}
