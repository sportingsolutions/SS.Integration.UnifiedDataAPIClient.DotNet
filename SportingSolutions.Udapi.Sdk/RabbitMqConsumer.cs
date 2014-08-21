using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace SportingSolutions.Udapi.Sdk
{
    public class RabbitMqConsumer : DefaultBasicConsumer
    {

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            
        }
    }
}
