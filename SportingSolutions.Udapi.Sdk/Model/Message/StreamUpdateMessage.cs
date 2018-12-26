using System;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class StreamUpdateMessage
    {
        public Guid MessageId { get; set; }
        public string Id { get; set; }
        public string Message { get; set; }
	    public DateTime ReceivedAt { get; set; }
	}
}