using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Actors
{
    public class StopStreamingMessage
    {
        public IConsumer Resource { get; set; }
    }
}