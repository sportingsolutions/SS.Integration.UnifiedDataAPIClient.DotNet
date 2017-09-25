using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class RemoveConsumerMessage
    {
        public IConsumer Consumer { get; set; }
    }
}