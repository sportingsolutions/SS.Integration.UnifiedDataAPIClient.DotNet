using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class NewConsumerMessage
    {
        public IConsumer Consumer { get; set; }
    }
}