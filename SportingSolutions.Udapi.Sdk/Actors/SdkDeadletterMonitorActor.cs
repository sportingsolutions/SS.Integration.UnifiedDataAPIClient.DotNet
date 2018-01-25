using Akka.Actor;
using Akka.Event;
using log4net;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    // A dead letter handling actor specifically for messages of type "DeadLetter"
    public class SdkDeadletterMonitorActor : ReceiveActor
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(SdkDeadletterMonitorActor));

        public SdkDeadletterMonitorActor()
        {
            Receive<DeadLetter>(dl => HandleDeadletter(dl));
        }

        private void HandleDeadletter(DeadLetter dl)
        {
            _logger.Debug($"DeadLetter captured: {dl.Message}, sender: {dl.Sender}, recipient: {dl.Recipient}");
        }
    }
}
