using System;
using System.Collections.Generic;
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
        
        private readonly Dictionary<string, ResourceSubscriber> _subscribers;

        internal int SubscribersCount => _subscribers.Count;
        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateDispatcherActor));

        public UpdateDispatcherActor()
        {
            _subscribers = new Dictionary<string, ResourceSubscriber>();
            Receive<StreamUpdateMessage>(message => ProcessMessage(message));
            
            Receive<DisconnectMessage>(message => Disconnect(message));
            Receive<NewSubscriberMessage>(message => AddSubscriber(message.Subscriber));
            Receive<RemoveSubscriberMessage>(message => RemoveSubscriber(message.Subscriber));

            Receive<RetrieveSubscriberMessage>(x => AskForSubscriber(x.Id));
            Receive<SubscribersCountMessage>(x => AskSubsbscribersCount());
            Receive<RemoveAllSubscribers>(x => RemoveAll());

            Receive<DisposeMessage>(x => Dispose());

            _logger.Info("UpdateDispatcherActor was created");
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
            if (!_subscribers.ContainsKey(message.Id)) return;

            try
            {
                var subscriber = _subscribers[message.Id];
                _subscribers.Remove(message.Id);

                subscriber.Resource.Tell(message);

                _logger.Debug(
                    $"subscriberId={message.Id} removed from UpdateDispatcherActor and stream disconnected; subscribersCount={_subscribers.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error($"failed removing/disconnecting subscriberId={message.Id}", ex);
            }
        }

        private void Connect(IStreamSubscriber subscriber)
        {
            var newResourceSubscriber = new ResourceSubscriber
            {
                Resource = Context.ActorOf(Props.Create<ResourceActor>(subscriber.Consumer)),
                StreamSubscriber = subscriber
            };

            _subscribers[subscriber.Consumer.Id] = newResourceSubscriber;
            newResourceSubscriber.Resource.Tell(new ConnectMessage() { Id = subscriber.Consumer.Id, Consumer = subscriber.Consumer} );
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
            else if (_subscribers.ContainsKey(message.Id))
            {
                //stream update is passed to the resource
                _subscribers[message.Id].Resource.Tell(message);
            }
            else
            {
                _logger.Warn($"ProcessMessage subscriberId={message.Id} was not found");
            }
        }

        private void AddSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;
            
            Connect(subscriber);
            
            Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new NewSubscriberMessage {Subscriber = subscriber} );
            
            _logger.Info($"consumerId={subscriber.Consumer.Id} added to the dispatcher, count={_subscribers.Count}");
        }

        private void AskSubsbscribersCount()
        {
            Sender.Tell(_subscribers.Count);
        }

        private void AskForSubscriber(string subscriberId)
        {
            ResourceSubscriber resourceSubscriber;
            if (_subscribers.TryGetValue(subscriberId, out resourceSubscriber) && resourceSubscriber != null)
                Sender.Tell(resourceSubscriber.StreamSubscriber);
            else
                Sender.Tell(new NotFoundMessage());
        }

        private void RemoveSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;

            _logger.DebugFormat($"Removing consumerId={subscriber.Consumer.Id} from dispatcher");

            ResourceSubscriber resourceSubscriber;
            _subscribers.TryGetValue(subscriber.Consumer.Id, out resourceSubscriber);
            if (resourceSubscriber == null)
            {
                _logger.WarnFormat(
                    $"consumerId={subscriber.Consumer.Id} can't be removed from the dispatcher as it was not found.");
                return;
            }

            try
            {
                var disconnectMsg = new DisconnectMessage {Id = subscriber.Consumer.Id};
                Self.Tell(disconnectMsg);
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new RemoveSubscriberMessage {Subscriber = subscriber});
            }
            catch (Exception ex)
            {
                _logger.Error($"RemoveSubscriber for consumerId={subscriber.Consumer?.Id ?? "null"} failed", ex);
            }
        }

        private void RemoveAll()
        {
            if (_subscribers.Count > 0)
            {
                _logger.DebugFormat("Sending disconnection to count={0} consumers", _subscribers.Count);
                _subscribers.Values.ForEach(
                    x => Self.Tell(new DisconnectMessage {Id = x.StreamSubscriber.Consumer.Id}));
                _logger.Info("All consumers are notified about disconnection");
            }
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
            internal IStreamSubscriber StreamSubscriber { get; set; }
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

    internal class RemoveAllSubscribers
    {
    }

    #endregion 
}
