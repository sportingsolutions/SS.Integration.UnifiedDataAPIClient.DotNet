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
using System.Text;
using log4net;
using RabbitMQ.Client;
using SportingSolutions.Udapi.Sdk.Interfaces;


namespace SportingSolutions.Udapi.Sdk
{

    internal class StreamSubscriber : DefaultBasicConsumer, IStreamSubscriber
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));

        public StreamSubscriber(IModel model, IConsumer consumer, IDispatcher dispatcher)
            : base(model)
        {
            Consumer = consumer;
            ConsumerTag = consumer.Id;
            Dispatcher = dispatcher;
        }


        public void StartConsuming(string queueName)
        {
            try
            {
                Model.BasicConsume(queueName, true, Consumer.Id, this);
                Dispatcher.AddSubscriber(this);
            }
            catch(Exception e)
            {
                _logger.Error("An error occured while trying to start streaming for consumerId=" + Consumer.Id, e);
                throw;
            }
        }

        public void StopConsuming()
        {
            try
            {
                Model.BasicCancel(ConsumerTag);
            }
            catch (Exception e)
            {
                _logger.Error("An error occured while stoping streaming for consumedId=" + ConsumerTag, e);
            }
            finally
            {
                Dispatcher.RemoveSubscriber(this);
                _logger.InfoFormat("Streaming stopped for consumerId={0}", ConsumerTag);
            }
        }

        public IConsumer Consumer { get; private set; }

        public IDispatcher Dispatcher { get; private set; }

        #region DefaultBasicConsumer

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            if(!IsRunning)
                return;

            Dispatcher.DispatchMessage(consumerTag, Encoding.UTF8.GetString(body));
        }

        public override void HandleModelShutdown(IModel model, ShutdownEventArgs reason)
        {
            _logger.InfoFormat("Model shutdown for consumerId={0} - disconnection event will be raised", ConsumerTag);

            base.HandleModelShutdown(model, reason);
            Dispatcher.RemoveSubscriber(this);
        }

        #endregion
        
    }
}
