using System;
using Akka.Actor;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class ErrorControllerActor : ReceiveActor
    {
        public event EventHandler<SdkErrorArgs> ErrorOcured;

        public const string ActorName = "ErrorControllerActor";

        public ErrorControllerActor()
        {
            Receive<CriticalActorRestartedMessage>(message => OnActorRestarted(message, true));
        }

        //public 

        public void OnActorRestarted(CriticalActorRestartedMessage message, bool isCritical)
        {
            ErrorOcured?.Invoke(this, new SdkErrorArgs($"Actor restarted {message.ActorName}", isCritical));
        }
    }
}
