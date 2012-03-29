using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabbitMQ.Client;

namespace SportingSolutions.Udapi.Sdk
{
    public class QueueingCustomConsumer : QueueingBasicConsumer
    {
        public QueueingCustomConsumer(IModel model) : base(model)
        {

        }

        public override void HandleBasicCancel(string consumerTag)
        {
            QueueCancelled();
        }

        public event Action QueueCancelled;

    }
}
