using Akka.Actor;
using SportingSolutions.Udapi.Sdk.Model.Message;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    public class StupidActor : ReceiveActor
    {
        public StupidActor()
        {
            Receive<NewConsumerMessage>(x=>  ReceiveMessage(x));
        }

        internal void ReceiveMessage(NewConsumerMessage msg)
        {
            Sender.Tell(new ConnectStreamMessage());
        }

    }
}
