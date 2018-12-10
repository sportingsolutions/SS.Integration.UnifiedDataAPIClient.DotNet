using System;
using System.Threading;
using Akka.Actor;
using Akka.TestKit;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    public class EchoControllerActorTest : SdkTestKit
    {
	    private const string Id1 = "ID1";
	    private const string Id2 = "ID2";


		[SetUp]
        public void Initialise()
        {
            SdkActorSystem.InitializeActors = false;
            ((Configuration)UDAPI.Configuration).UseEchos = true;
            ((Configuration)UDAPI.Configuration).EchoWaitInterval = int.MaxValue;
        }

        private EchoControllerActor GetEchoControllerActorWith1Consumer(string consumerId)
        {
            var testing = new EchoControllerActor();

            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subscriber.Object);

            return testing;
        }

        private void AddConsumer(EchoControllerActor actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.AddConsumer(subscriber.Object);

        }

        private TestActorRef<EchoControllerActor> GetMockedEchoControllerActorWith1Consumer(string consumerId)
        {
	        var testingProps = Props.Create(() => new EchoControllerActor());
	        var testing = ActorOfAsTestActorRef<EchoControllerActor>(testingProps);

			Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);
	        consumer.Setup(x => x.SendEcho()).Callback(() => {});

			Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);




	        testing.UnderlyingActor.AddConsumer(subscriber.Object);

            return testing;
        }

        private void AddConsumer(TestActorRef<EchoControllerActor> actor, string consumerId)
        {
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(consumerId);
	        consumer.Setup(x => x.SendEcho()).Callback(() => { });

			Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);

            actor.UnderlyingActor.AddConsumer(subscriber.Object);

        }

        [Test]
        public void AddConsumerWithNullTest()
        {
            var testing = new EchoControllerActor();
            testing.AddConsumer(null);
            testing.ConsumerCount.Should().Be(0);
        }

        [Test]
        public void Add1ConsumerTest()
        {
	        
	        
			var testing = new EchoControllerActor();
            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subscriber.Object);
            testing.ConsumerCount.Should().Be(1);
            

        }

        [Test]
        public void AddSameConsumerTest()
        {
	        
	        
			var testing = GetEchoControllerActorWith1Consumer(Id1);
            AddConsumer(testing, Id1);
            testing.ConsumerCount.Should().Be(1);
        }

        [Test]
        public void RemoveConsumerPositiveTest()
        {
			var testing = new EchoControllerActor();

            
            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);



            testing.AddConsumer(subscriber.Object);
            testing.ConsumerCount.Should().Be(1);

            testing.RemoveConsumer(subscriber.Object);
            testing.ConsumerCount.Should().Be(0);

        }

        [Test]
        public void RemoveConsumerNegativeTest()
        {
	     	var testing = new EchoControllerActor();

            Mock<IConsumer> consumer1 = new Mock<IConsumer>();
            consumer1.Setup(x => x.Id).Returns(Id1);

            Mock<IConsumer> consumer2 = new Mock<IConsumer>();
            consumer2.Setup(x => x.Id).Returns(Id2);


            Mock<IStreamSubscriber> subscriber1 = new Mock<IStreamSubscriber>();
            subscriber1.Setup(x => x.Consumer).Returns(consumer1.Object);

            Mock<IStreamSubscriber> subscriber2 = new Mock<IStreamSubscriber>();
            subscriber2.Setup(x => x.Consumer).Returns(consumer2.Object);


            testing.AddConsumer(subscriber1.Object);
            testing.ConsumerCount.Should().Be(1);
            testing.RemoveConsumer(subscriber2.Object);
            testing.ConsumerCount.Should().Be(1);

        }

        [Test]
        public void GetDefaultEchosCountDownTest()
        {
	        
	        var testing = new EchoControllerActor();


            Mock<IConsumer> consumer = new Mock<IConsumer>();
            consumer.Setup(x => x.Id).Returns(Id1);


            Mock<IStreamSubscriber> subscriber = new Mock<IStreamSubscriber>();
            subscriber.Setup(x => x.Consumer).Returns(consumer.Object);
            
            testing.AddConsumer(subscriber.Object);
            testing.GetEchosCountDown(Id1).Should().Be(UDAPI.Configuration.MissedEchos);
        }

        [Test]
        public void CheckEchosDecteaseEchosCountDownTest()
        {
	        
	        
			var testing = GetMockedEchoControllerActorWith1Consumer(Id1);
            var message = new EchoControllerActor.SendEchoMessage();

            testing.Tell(message);
            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.GetEchosCountDown(Id1).Should().Be(UDAPI.Configuration.MissedEchos - 1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void CheckEchosUntillAllSubscribersClearTest()
        {
	        
	        
			var testing = GetMockedEchoControllerActorWith1Consumer(Id1);
            AddConsumer(testing, Id2);

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.Should().Be(2);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            var message = new EchoControllerActor.SendEchoMessage();
            for (int i = 0; i < UDAPI.Configuration.MissedEchos + 1; i++)
            {
                testing.Tell(message);
            }

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.Should().Be(0);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        public void CheckEchosWithProcessEchoClearTest()
        {
	        
	        

			var testing = GetMockedEchoControllerActorWith1Consumer(Id1);
            AddConsumer(testing, Id2);

            AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.Should().Be(2);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
            var echoMessage = new EchoMessage() {Id = Id2 };

            testing.Tell(sendEchoMessage);
            
            testing.Tell(echoMessage);

            testing.Tell(sendEchoMessage);
            
            testing.Tell(echoMessage);

	        AwaitAssert(() =>
		        {
					testing.UnderlyingActor.GetEchosCountDown(Id2).Should().Be(UDAPI.Configuration.MissedEchos);
				},
		        TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
		        TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));


			for (int i = 0; i < UDAPI.Configuration.MissedEchos-1; i++)
            {
                testing.Tell(sendEchoMessage);
            }

	        AwaitAssert(() =>
                {
                    testing.UnderlyingActor.ConsumerCount.Should().Be(1);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

	        testing.UnderlyingActor.GetEchosCountDown(Id2).Should().Be(1);
		}

	    [Test]
	    public void SendEchoCallTest()
	    {
		    var testingProps = Props.Create(() => new EchoControllerActor());
		    var testing = ActorOfAsTestActorRef<EchoControllerActor>(testingProps);

			//var authProps = Props.Create(() => new EchoControllerActor());
			//var testing = Sys.ActorOf(authProps);

			//var system = new ActorSystem();

			//var probe = this.CreateTestProbe();
		    //var testing = Sys.ActorOf<EchoControllerActor>();

			//var testing = ActorOfAsTestActorRef<EchoControllerActor>(() => new EchoControllerActor(), EchoControllerActor.ActorName);
		    
		    Mock<IConsumer> consumer1 = new Mock<IConsumer>();
		    consumer1.Setup(x => x.Id).Returns(Id1);
		    int sendEchoCallCount = 0;
		    consumer1.Setup(x => x.SendEcho()).Callback(() => sendEchoCallCount++);


		    Mock<IConsumer> consumer2 = new Mock<IConsumer>();
		    consumer2.Setup(x => x.Id).Returns(Id2);
		    consumer2.Setup(x => x.SendEcho()).Callback(() => sendEchoCallCount++);


		    Mock<IStreamSubscriber> subscriber1 = new Mock<IStreamSubscriber>();
		    subscriber1.Setup(x => x.Consumer).Returns(consumer1.Object);


		    Mock<IStreamSubscriber> subscriber2 = new Mock<IStreamSubscriber>();
		    subscriber2.Setup(x => x.Consumer).Returns(consumer2.Object);

		    testing.Tell(new NewSubscriberMessage() {Subscriber = subscriber1.Object});
		    testing.Tell(new NewSubscriberMessage() {Subscriber = subscriber2.Object});

		    AwaitAssert(() => { testing.UnderlyingActor.ConsumerCount.Should().Be(2); },
			    TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
			    TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

		    var sendEchoMessage = new EchoControllerActor.SendEchoMessage();
		    var echoMessage = new EchoMessage {Id = Id2 };

		    testing.Tell(sendEchoMessage);
		    testing.Tell(echoMessage);

		    for (int i = 0; i < UDAPI.Configuration.MissedEchos; i++)
		    {
			    testing.Tell(sendEchoMessage);
			    testing.Tell(echoMessage);
		    }


			AwaitAssert(() => { testing.UnderlyingActor.ConsumerCount.Should().Be(1); },
			    TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
			    TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

		    sendEchoCallCount.Should().Be(UDAPI.Configuration.MissedEchos + 1);
		    
	    }



    }
}
