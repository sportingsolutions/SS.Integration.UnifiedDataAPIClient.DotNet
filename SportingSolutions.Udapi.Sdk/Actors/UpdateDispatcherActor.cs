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
        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateDispatcherActor));

        public UpdateDispatcherActor()
        {
            _subscribers = new Dictionary<string, ResourceSubscriber>();
            Receive<StreamUpdateMessage>(message => ProcessMessage(message));
            Receive<DisconnectMessage>(message => Disconnect(message));
            Receive<NewConsumerMessage>(message => NewConsumerMessageHandler(message));
            Receive<SubscribersCountMessage>(x => AskSubsbscribersCount());
            Receive<RemoveAllSubscribers>(x => RemoveAll());
            Receive<DisposeMessage>(x => Dispose());
        }

        private void Disconnect(DisconnectMessage message)
        {
            if (!_subscribers.ContainsKey(message.Id)) return;

            _subscribers[message.Id].Resource.Tell(message);
            _subscribers.Remove(message.Id);
            _logger.DebugFormat("subscriberId={0} removed from UpdateDispatcherActor", message.Id);

            if (_subscribers.Count == 0)
                Sender.Tell(new AllSubscribersDisconnectedMessage());
        }

        private void ProcessMessage(StreamUpdateMessage message)
        {
            // is this an echo message?
            if (message.Message.StartsWith("{\"Relation\":\"http://api.sportingsolutions.com/rels/stream/echo\""))
            {
                _logger.DebugFormat($"Echo arrived with {message.Id}");
                Context.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new EchoMessage { Id = message.Id, Message = message.Message });
            }
            else if (_subscribers.ContainsKey(message.Id))
            {
                _subscribers[message.Id].Resource.Tell(message);
            }
            else
                _logger.Warn($"The subscriber with id {message.Id} couldn't be found");
        }

        private void NewConsumerMessageHandler(NewConsumerMessage message)
        {
            var resourceActor = Context.ActorOf(Props.Create<ResourceActor>(message.Consumer));
            if (!_subscribers.ContainsKey(message.Consumer.Id))
            {
                _subscribers.Add(message.Consumer.Id, new ResourceSubscriber
                {
                    Resource = resourceActor,
                    StreamSubscriber = null
                });
            }
            resourceActor.Tell(new ConnectMessage() { Id = message.Consumer.Id, Consumer = message.Consumer });
        }

        private void AskSubsbscribersCount()
        {
            Sender.Tell(_subscribers.Count);
        }

        private void RemoveAll()
        {
            _subscribers.Clear();
            Sender.Tell(new AllSubscribersDisconnectedMessage());
            _logger.Info("All consumers have been removed");
        }

        private void Dispose()
        {
            _logger.DebugFormat("Disposing dispatcher");

            try
            {
                RemoveAll();
            }
            finally
            {
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new DisposeMessage());
            }
        }

        private class ResourceSubscriber
        {
            internal IActorRef Resource { get; set; }
            internal IStreamSubscriber StreamSubscriber { get; set; }
        }
    }

    #region Messages specific to UpdateDispatcherActor (non resusable by other actors)

    //Message used to get count if subscribers
    internal class SubscribersCountMessage
    {

    }

    internal class RemoveAllSubscribers
    {

    }

    internal class AllSubscribersDisconnectedMessage
    {

    }

    #endregion 
}
