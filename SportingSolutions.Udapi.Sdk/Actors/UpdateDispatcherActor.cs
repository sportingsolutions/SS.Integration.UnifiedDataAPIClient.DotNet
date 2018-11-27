using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Util.Internal;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class UpdateDispatcherActor : ReceiveActor
    {
        public const string ActorName = "UpdateDispatcherActor";
        
        private readonly Dictionary<string, ResourceSubscriber>  _consumers;
	    private ICancelable deleteAllCancel;

		internal int SubscribersCount => _consumers.Count;
        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateDispatcherActor));

        public UpdateDispatcherActor()
        {
            _consumers = new Dictionary<string, ResourceSubscriber>();
            Receive<StreamUpdateMessage>(message => ProcessMessage(message));
            
            Receive<DisconnectMessage>(message => Disconnect(message));
            Receive<NewSubscriberMessage>(message => AddSubscriber(message));
	        Receive<NewConsumerMessage>(message => AddConsumer(message));
	        Receive<RemoveConsumerMessage>(message => RemoveConsumer(message));
			Receive<RemoveSubscriberMessage>(message => RemoveSubscriber(message.Subscriber));
			
			//Receive<RetrieveSubscriberMessage>(x => AskForSubscriber(x.Id));
			Receive<SubscribersCountMessage>(x => AskSubsbscribersCount());
            Receive<DisconnectionAccuredMessage>(x => HandleDisconnection());
	        Receive<ReconsumedQueueMessage>(x => ReconsumedQueue());
			Receive<RemoveAllMessage>(x => RemoveAll());

			Receive<DisposeMessage>(x => Dispose());

			_logger.Info("UpdateDispatcherActor was created");
		}

	    
	    private void RemoveConsumer(RemoveConsumerMessage message)
	    {
		    if (_consumers.ContainsKey(message.Consumer.Id))
		    {
			    _consumers.Remove(message.Consumer.Id);
			    _logger.Info($"consumerId={message.Consumer.Id} was deleted from the dispatcher, count={_consumers.Count}");
			}
		    else
		    {
				_logger.Info($"consumerId={message.Consumer.Id} was not found at the  dispatcher, count={_consumers.Count}");
			}
	    }

	    private void ReconsumedQueue()
	    {
		    if (deleteAllCancel != null)

		    {
			    _logger.Info("RemoveAll was cancelled as queue consuming was resumed");
				deleteAllCancel.Cancel();
			    deleteAllCancel = null;
			}

    }

	    private void AddConsumer(NewConsumerMessage message)
	    {
			Connect(message.Consumer);
		    _logger.Info($"consumerId={message.Consumer.Id} added to the dispatcher, count={_consumers.Count}");
		}

	    protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        private void Disconnect(DisconnectMessage message)
        {
            _logger.DebugFormat($"subscriberId={message.Id} disconnect message received");
            if (!_consumers.ContainsKey(message.Id)) return;

            try
            {
                var subscriber = _consumers[message.Id];
                _consumers.Remove(message.Id);

                subscriber.Resource.Tell(message);
                //subscriber.StreamSubscriber.StopConsuming();

                _logger.Debug(
                    $"subscriberId={message.Id} removed from UpdateDispatcherActor and stream disconnected; subscribersCount={_consumers.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error($"failed removing/disconnecting subscriberId={message.Id}", ex);
            }
        }

        private void Connect(IConsumer consumer)
        {
            var newResourceSubscriber = new ResourceSubscriber
            {
                Resource = Context.ActorOf(Props.Create<ResourceActor>(consumer)),
                //StreamSubscriber = subscriber
            };

	        if (!_consumers.Any())
	        {
		        var msg = consumer.RequestEchoUrl();
		        Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(msg);
			}

            _consumers[consumer.Id] = newResourceSubscriber;
            newResourceSubscriber.Resource.Tell(new ConnectMessage() { Id = consumer.Id, Consumer = consumer} );
        }

        private void ProcessMessage(StreamUpdateMessage message)
        {
            // is this an echo message?
            if (message.Message.StartsWith("{\"Relation\":\"http://api.sportingsolutions.com/rels/stream/echo\""))
            {
                _logger.DebugFormat($"Echo arrived for fixtureId={message.Id}");
                Context.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new EchoMessage {Id = message.Id, Message = message.Message});
            }
            else if (_consumers.ContainsKey(message.Id))
            {
                //stream update is passed to the resource
                _consumers[message.Id].Resource.Tell(message);
            }
            else
            {
                _logger.Warn($"ProcessMessage subscriberId={message.Id} was not found message=\"{(message.Message.Length > 200 ? message.Message.Substring(0, 200): message.Message)} ...\"");
            }
        }

        private void AddSubscriber(NewSubscriberMessage message)
        {
            Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(message );
        }

        private void AskSubsbscribersCount()
        {
            Sender.Tell(_consumers.Count);
        }

        //private void AskForSubscriber(string subscriberId)
        //{
        //    ResourceSubscriber resourceSubscriber;
        //    if (_consumers.TryGetValue(subscriberId, out resourceSubscriber) && resourceSubscriber != null)
        //        Sender.Tell(resourceSubscriber.StreamSubscriber);
        //    else
        //        Sender.Tell(new NotFoundMessage());
        //}

        private void RemoveSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;

            ResourceSubscriber resourceSubscriber = null;
            //_consumers.TryGetValue(subscriber.Consumer.Id, out resourceSubscriber);
            if (resourceSubscriber == null)
            {
                _logger.WarnFormat(
                    $"consumer can't be removed from the dispatcher as it was not found.");
                return;
            }

            try
            {
                //todo review

	            //var disconnectMsg = new DisconnectMessage {Id = subscriber.Consumer.Id};
                //Self.Tell(disconnectMsg);
                //Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                //    .Tell(new RemoveSubscriberMessage {Subscriber = subscriber});
            }
            catch (Exception ex)
            {
                _logger.Error($"RemoveSubscriber for consumer failed {ex}");
            }
        }

        private void HandleDisconnection()
        {

			if (deleteAllCancel == null)

			{
				deleteAllCancel =  SdkActorSystem.ActorSystem.Scheduler
				.ScheduleTellOnceCancelable(
				TimeSpan.FromSeconds(10),
				Self, new RemoveAllMessage(), Self);
			}
			else
			{
				RemoveAll();
			}
		}

	    private void RemoveAll()
	    {
		    if (_consumers.Count > 0)
		    {
			    _logger.DebugFormat("Sending disconnection to count={0} consumers", _consumers.Count);
			    _consumers.Keys.ForEach(x => Self.Tell(new DisconnectMessage { Id = x }));
			    _logger.Info("All consumers are notified about disconnection");
		    }

			_consumers.Clear();

		    deleteAllCancel = null;

	    }

		private void Dispose()
        {
            _logger.DebugFormat("Disposing dispatcher");

            try
            {
	            RemoveAll();
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new DisposeMessage());
            }
            catch(Exception ex)
            {
                _logger.Error("Dispose failed", ex);
            }
        }

        private class ResourceSubscriber
        {
            internal IActorRef Resource { get; set; }
            //internal IStreamSubscriber StreamSubscriber { get; set; }
        }
    }

    #region Messages specific to UpdateDispatcherActor (non resusable by other actors)

    internal class RetrieveSubscriberMessage
    {
        public string Id { get; set; }
    }

    //Message used to get count if subscribers
    internal class SubscribersCountMessage
    {
    }

    internal class DisconnectionAccuredMessage
    {
    }

	internal class RemoveAllMessage
	{
	}

	internal class ReconsumedQueueMessage
	{
	}

	

	#endregion
}
