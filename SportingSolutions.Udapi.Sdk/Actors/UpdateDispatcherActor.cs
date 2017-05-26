using System.Collections.Generic;
using Akka.Actor;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class UpdateDispatcherActor : ReceiveActor
    {
        public const string ActorName = "UpdateDispatcherActor";
        
        private readonly Dictionary<string, ResourceConsumer> _consumers;

        internal int SubscribersCount => _consumers.Count;
        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateDispatcherActor));

        public UpdateDispatcherActor()
        {
            _consumers = new Dictionary<string, ResourceConsumer>();
            Receive<StreamUpdateMessage>(message => ProcessMessage(message));
            Receive<DisconnectMessage>(message => Disconnect(message));
            Receive<NewConsumerMessage>(message => NewConsumerMessageHandler(message));
            Receive<ConsumersCountMessage>(x => AskConsumersCount());
            Receive<RemoveAllConsumersMessage>(x => RemoveAll());
            Receive<DisposeMessage>(x => Dispose());
        }

        private void Disconnect(DisconnectMessage message)
        {
            if (!_consumers.ContainsKey(message.Id)) return;

            var consumer = _consumers[message.Id];
            _consumers.Remove(message.Id);
            consumer.Resource.Tell(message);

            _logger.DebugFormat("subscriberId={0} removed from UpdateDispatcherActor", message.Id);

            if (_consumers.Count == 0)
                Sender.Tell(new AllConsumersDisconnectedMessage());
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
            else if (_consumers.ContainsKey(message.Id))
            {
                _consumers[message.Id].Resource.Tell(message);
            }
            else
                _logger.Warn($"The subscriber with id {message.Id} couldn't be found");
        }

        private void NewConsumerMessageHandler(NewConsumerMessage message)
        {
            var resourceActor = Context.ActorOf(Props.Create<ResourceActor>(message.Consumer));
            _consumers[message.Consumer.Id] = new ResourceConsumer {Resource = resourceActor};
            resourceActor.Tell(new ConnectMessage() { Id = message.Consumer.Id, Consumer = message.Consumer });
        }

        private void AskConsumersCount()
        {
            Sender.Tell(_consumers.Count);
        }

        private void RemoveAll()
        {
            _consumers.Clear();
            Sender.Tell(new AllConsumersDisconnectedMessage());
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

        private class ResourceConsumer
        {
            internal IActorRef Resource { get; set; }
            internal IConsumer Consumer { get; set; }
        }
    }

    #region Messages specific to UpdateDispatcherActor (non resusable by other actors)

    //Message used to get count if subscribers
    internal class ConsumersCountMessage
    {

    }

    internal class AllConsumersDisconnectedMessage
    {

    }

    #endregion 
}
