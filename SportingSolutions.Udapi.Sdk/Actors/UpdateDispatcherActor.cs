using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.IO;
using Akka.Util.Internal;
using log4net;
using log4net.Config;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class UpdateDispatcherActor : ReceiveActor
    {
        public const string ActorName = "UpdateDispatcherActor";
        
        private readonly Dictionary<string, ResourceSubscriber> _subscribers;

        internal int SubscribersCout => _subscribers.Count;
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
        }

        

        private void Disconnect(DisconnectMessage message)
        {
            if (!_subscribers.ContainsKey(message.Id)) return;

            var subscriber = _subscribers[message.Id];
            _subscribers.Remove(message.Id);

            subscriber.Resource.Tell(message);
            subscriber.StreamSubscriber.StopConsuming();

            _logger.DebugFormat("subscriberId={0} removed from UpdateDispatcherActor", message.Id);
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
            if (!_subscribers.ContainsKey(message.Id))
            {
                //_subscribers[message.Id] = Context.ActorOf(Props.Create<ResourceActor>());
                throw new InvalidOperationException($"Subscriber doesn't exist for {message.Id}");
            }

            // is this an echo message?
            if (message.Message.StartsWith("{\"Relation\":\"http://api.sportingsolutions.com/rels/stream/echo\""))
            {

                _logger.DebugFormat($"Echo arrived with {message.Id}");
                Context.ActorSelection(SdkActorSystem.EchoControllerActorPath)
                    .Tell(new EchoMessage {Id = message.Id, Message = message.Message});
            }
            else
            {
                //stream update is passed to the resource
                _subscribers[message.Id].Resource.Tell(message);
            }
        }

        private void AddSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;
            
            Connect(subscriber);
            
            Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new NewSubscriberMessage {Subscriber = subscriber} );
            
            //TODO : Fix logging
            _logger.InfoFormat($"consumerId={subscriber.Consumer.Id} added to the dispatcher, count={_subscribers.Count}");
        }

        private void AskSubsbscribersCount()
        {
            Sender.Tell(_subscribers.Count);
        }

        private void AskForSubscriber(string subscriberId)
        {
            ResourceSubscriber resourceSubscriber  = null;
            if (_subscribers.TryGetValue(subscriberId, out resourceSubscriber) && resourceSubscriber != null)
                Sender.Tell(resourceSubscriber.StreamSubscriber);
            else
                Sender.Tell(new NotFoundMessage());
        }

        private void RemoveSubscriber(IStreamSubscriber subscriber)
        {
            if (subscriber == null)
                return;

            ResourceSubscriber resourceSubscriber = null;
            _subscribers.TryGetValue(subscriber.Consumer.Id, out resourceSubscriber);
            if (resourceSubscriber == null)
            {
                _logger.WarnFormat($"consumerId={subscriber.Consumer.Id} can't be removed from the dispatcher cause it was not found. _subscribers.ContainsKey({subscriber.Consumer.Id})={_subscribers.ContainsKey(subscriber.Consumer.Id)} ");
                return;
            }

            try
            {
                var disconnectMsg = new DisconnectMessage { Id = subscriber.Consumer.Id };
                Self.Tell(disconnectMsg);
               
                _logger.InfoFormat($"consumerId={subscriber.Consumer.Id} removed from the dispatcher, count={_subscribers.Count}");
            }
            finally
            {
                Context.System.ActorSelection(SdkActorSystem.EchoControllerActorPath).Tell(new RemoveSubscriberMessage { Subscriber = subscriber } );
            }
        }

        private void RemoveAll()
        {
            try
            {
                _logger.DebugFormat("Sending disconnection to count={0} consumers", _subscribers.Count);
                _subscribers.Values.ForEach(x=> Self.Tell(new DisconnectMessage() { Id = x.StreamSubscriber.Consumer.Id } ));
                _logger.Info("All consumers are notified about disconnection");

            }
            finally
            {
                //TODO: FIX EchoController
                //EchoManager.RemoveAll();

                //_subscribers.Clear(); 
                // = new Dictionary<string, ResourceSubscriber>();
                
            }
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
