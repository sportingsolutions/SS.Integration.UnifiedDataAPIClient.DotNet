using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class DisconnectMessage
    {
        public IConsumer Consumer { get; set; }
        public string Id { get; set; }
    }
}