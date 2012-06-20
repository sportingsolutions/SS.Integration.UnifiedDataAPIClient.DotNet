using System;
using RabbitMQ.Client;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class QueueingCustomConsumer : QueueingBasicConsumer
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(QueueingCustomConsumer).ToString());

        public QueueingCustomConsumer(IModel model) : base(model)
        {

        }

        public override void HandleBasicCancel(string consumerTag)
        {
            base.HandleBasicCancel(consumerTag);
            _logger.Debug("HandleBasicCancel");
        }

        public override void HandleBasicCancelOk(string consumerTag)
        {
            base.HandleBasicCancelOk(consumerTag);
            _logger.Debug("HandleBasicCancelOk");
            QueueCancelled();
        }

        public event Action QueueCancelled;

    }
}
