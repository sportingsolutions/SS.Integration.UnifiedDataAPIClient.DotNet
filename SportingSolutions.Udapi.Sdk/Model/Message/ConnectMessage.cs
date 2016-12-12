using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class ConnectMessage
    {
        public string Id { get; set; }
        public IConsumer Consumer { get; set; }
    }
}