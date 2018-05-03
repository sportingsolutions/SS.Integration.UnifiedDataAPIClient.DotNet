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
            _logger.Debug("StreamControlerActorMailBox.PriorityGenerator entry");
            if (message as DisconnectMessage != null)
            {
                _logger.Debug("StreamControlerActorMailBox.PriorityGenerator prioryty=0");
                return 0;
            }
            _logger.Debug("StreamControlerActorMailBox.PriorityGenerator prioryty=1");
            return 1;
        }
    }
}
