//Copyright 2012 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

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
            //base.HandleBasicCancel(consumerTag);
            _logger.Debug("HandleBasicCancel");

            if (QueueCancelledUnexpectedly != null)
            {
                QueueCancelledUnexpectedly(consumerTag);
            }
        }

        public override void HandleBasicCancelOk(string consumerTag)
        {
          //  base.HandleBasicCancelOk(consumerTag);
            _logger.Debug("HandleBasicCancelOk");
            if (QueueCancelled != null)
            {
                QueueCancelled(consumerTag);
            }
            else
            {
                _logger.Warn("HandleBasicCancelOk-QueueCancelled was null");
            }
        }

        public event Action<string> QueueCancelledUnexpectedly;
        public event Action<string> QueueCancelled;

    }
}
