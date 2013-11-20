using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model
{
    public class MessageUpdate : IMessageUpdate
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public bool IsEcho { get; set; }
    }
}
