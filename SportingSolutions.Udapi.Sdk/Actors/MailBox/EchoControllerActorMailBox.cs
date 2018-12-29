using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Dispatch;
using Akka.Actor;
using Akka.Configuration;
using log4net;

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
