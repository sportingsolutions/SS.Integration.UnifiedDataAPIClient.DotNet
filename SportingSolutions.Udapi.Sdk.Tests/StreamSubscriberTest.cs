using System.Reflection;
using Akka.Actor;
using FluentAssertions;
using log4net;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    class StreamSubscriberTest : SdkTestKit
    {
        [SetUp]
        public void Initialise()
        {
            SdkActorSystem.InitializeActors = false;
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
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

        [Test]
        public void StopConsumingShouldNotLogWarnWhenAlreadyClosedConnectionTest()
        {
            var model = new Mock<IModel>();
            var consumer = new Mock<IConsumer>();
            var actor = new Mock<IActorRef>();
            var logger = new Mock<ILog>();

            consumer.SetupGet(c => c.Id).Returns("Consumer1Id");

            var test = new StreamSubscriber(model.Object, consumer.Object, actor.Object);

            var loggerField = test.GetType().GetField("_logger", BindingFlags.Instance | BindingFlags.NonPublic);
            loggerField.SetValue(test, logger.Object);

            model.Setup(x => x.BasicConsume("test", true, consumer.Object.Id, test)).Callback(() => { });
            test.StartConsuming("test");

            model.SetupGet(m => m.IsClosed).Returns(true);
            model.Setup(x => x.BasicCancel(consumer.Object.Id)).Callback(() =>
            {
                throw new AlreadyClosedException(
                    new ShutdownEventArgs(ShutdownInitiator.Application, 0, "Already closed"));
            });

            test.StopConsuming();

            logger.Verify(
                l => l.Warn(It.Is<string>(
                    msg => msg.StartsWith($"Connection already closed for consumerId={consumer.Object.Id}"))),
                Times.Never, "Connection already closed warning should not be logged.");
        }
    }
}
