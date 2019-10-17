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
using Akka.Actor;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;


namespace SportingSolutions.Udapi.Sdk
{

    internal class StreamSubscriber : DefaultBasicConsumer, IStreamSubscriber
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));

        internal bool IsStreamingStopped => Model == null || !Model.IsOpen || _isCanceled;
        private bool _isCanceled = false;

        public StreamSubscriber(IModel model, IConsumer consumer, IActorRef dispatcher)
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
                Model.BasicConsume(queueName, true, ConsumerTag, this);
                _isCanceled = false;
                //_logger.Debug($"Streaming started for consumerId={ConsumerTag}");
            }
            catch (Exception e)
            {
                _logger.Error("Error starting stream for consumerId=" + ConsumerTag, e);
                throw;
            }
        }

        public void StopConsuming()
        {
            try
            {
                if (!IsStreamingStopped)
                {
                    Model.BasicCancel(ConsumerTag);
                    _isCanceled = true;
                }
            }
            catch (AlreadyClosedException e)
            {
                _logger.Warn($"Connection already closed for consumerId={ConsumerTag} , \n {e}");
            }

            catch (ObjectDisposedException e)
            {
                _logger.Warn("Stopp stream called for already disposed object for consumerId=" + ConsumerTag, e);
            }

            catch (TimeoutException e)
            {
                _logger.Warn($"RabbitMQ timeout on StopStreaming for consumerId={ConsumerTag} {e}");
            }

            catch (Exception e)
            {
                _logger.Error("Error stopping stream for consumerId=" + ConsumerTag, e);
            }
            finally
            {
                Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = this });
                _logger.DebugFormat("Streaming stopped for consumerId={0}", ConsumerTag);
            }
        }

        public IConsumer Consumer { get; }

        public IActorRef Dispatcher { get; }

        #region DefaultBasicConsumer

        public override void HandleBasicConsumeOk(string consumerTag)
        {
            _logger.Debug($"HandleBasicConsumeOk consumerTag={consumerTag ?? "null"}");
            Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });

            base.HandleBasicConsumeOk(consumerTag);
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            if (!IsRunning)
                return;

            _logger.Debug(
                "HandleBasicDeliver" +
                $" consumerTag={consumerTag ?? "null"}" +
                $" deliveryTag={deliveryTag}" +
                $" redelivered={redelivered}" +
                $" exchange={exchange ?? "null"}" +
                $" routingKey={routingKey ?? "null"}" +
                (body == null ? " body=null" : $" bodyLength={body.Length}"));

            Dispatcher.Tell(new StreamUpdateMessage { Id = consumerTag, Message = Encoding.UTF8.GetString(body), ReceivedAt = DateTime.UtcNow });
        }

        public override void HandleBasicCancel(string consumerTag)
        {
            _logger.Debug($"HandleBasicCancel consumerTag={consumerTag ?? "null"}");
            base.HandleBasicCancel(consumerTag);
            Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = this });
        }

        #endregion


    }
}
