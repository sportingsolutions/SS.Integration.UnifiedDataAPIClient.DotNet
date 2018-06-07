using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using log4net;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Actors.MailBox
{
    public class StreamControllerActorMailBox: UnboundedPriorityMailbox
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamControllerActorMailBox));

        public StreamControllerActorMailBox(Settings settings, Config config) : base(settings, config)
        {

        }

        protected override int PriorityGenerator(object message)
        {
            var messageType = message.GetType();
            _logger.Debug($"StreamControlerActorMailBox.PriorityGenerator entry with messageType={messageType}");
            if (message is StreamControllerActor.DisconnectedMessage)
            {
                _logger.Debug($"StreamControlerActorMailBox.PriorityGenerator priority=0 with messageType={messageType}");
                return 0;
            }
            _logger.Debug($"StreamControlerActorMailBox.PriorityGenerator priority=1 with messageType={messageType}");
            return 1;
        }
    }
}
