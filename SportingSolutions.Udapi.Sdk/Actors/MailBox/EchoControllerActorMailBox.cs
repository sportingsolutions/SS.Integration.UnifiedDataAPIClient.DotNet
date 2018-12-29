using Akka.Dispatch;
using Akka.Actor;
using Akka.Configuration;


namespace SportingSolutions.Udapi.Sdk.Actors.MailBox
{
    public class EchoControllerActorMailBox : UnboundedPriorityMailbox
    {
        public EchoControllerActorMailBox(Settings settings, Config config) : base(settings, config)
        {
        }

        protected override int PriorityGenerator(object message)
        {
            if (message is SportingSolutions.Udapi.Sdk.Model.Message.EchoMessage)
                return 0;

            if (message is SportingSolutions.Udapi.Sdk.Actors.EchoControllerActor.SendEchoMessage)
                return 2;

            return 1;
        }
    }
}
