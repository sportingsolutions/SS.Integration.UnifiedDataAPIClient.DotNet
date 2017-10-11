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

    internal class StreamSubscriber : DefaultBasicConsumer, IStreamSubscriber, IDisposable
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));

        private bool _isDisposed;

        internal bool IsDisposed => _isDisposed;

        public StreamSubscriber(IModel model, IConsumer consumer, IActorRef dispatcher)
            : base(model)
        {
            Consumer = consumer;
            ConsumerTag = UDAPI.Configuration.UseSingleQueueStreamingMethod ? "StreamUpdates" : consumer.Id;
            Dispatcher = dispatcher;
            _isDisposed = false;
        }

        public void StartConsuming(string queueName)
        {
            try
            {
                var consumerTag = UDAPI.Configuration.UseSingleQueueStreamingMethod ? ConsumerTag : Consumer.Id;
                Model.BasicConsume(queueName, true, consumerTag, this);
            }
            catch (Exception e)
            {
                _logger.Error("Error starting stream for consumerId=" + Consumer.Id, e);
                throw;
            }
        }

        public void StopConsuming()
        {
            try
            {
                Model.BasicCancel(ConsumerTag);
            }
            catch (AlreadyClosedException e)
            {
                _logger.Warn($"Connection already closed for consumerId={ConsumerTag} , \n {e}");
            }

            catch (Exception e)
            {
                _logger.Error("Error stopping stream for consumerId=" + ConsumerTag, e);
            }
            finally
            {
                if (!UDAPI.Configuration.UseSingleQueueStreamingMethod)
                {
                    Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = this });
                }

                try
                {
                    Dispose();
                }
                catch { }

                _logger.DebugFormat("Streaming stopped for consumerId={0}", ConsumerTag);
            }
        }

        public IConsumer Consumer { get; private set; }

        public IActorRef Dispatcher { get; private set; }

        #region DefaultBasicConsumer

        public override void HandleBasicConsumeOk(string consumerTag)
        {
            if (!UDAPI.Configuration.UseSingleQueueStreamingMethod)
            {
                Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });
            }

            base.HandleBasicConsumeOk(consumerTag);
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            if (!IsRunning)
                return;

            var fixtureId = ExtractFixtureId(routingKey);

            Dispatcher.Tell(new StreamUpdateMessage
            {
                Id = UDAPI.Configuration.UseSingleQueueStreamingMethod ? (fixtureId ?? routingKey) : consumerTag,
                Message = Encoding.UTF8.GetString(body)
            });
        }

        public override void HandleBasicCancel(string consumerTag)
        {
            base.HandleBasicCancel(consumerTag);
            if (!UDAPI.Configuration.UseSingleQueueStreamingMethod)
            {
                Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = this });
            }
        }

        private string ExtractFixtureId(string routingKey)
        {
            var rks = routingKey.Split('.');
            return rks.Length > 2 ? routingKey.Split('.')[2] : null;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                if (Model != null)
                {
                    Model.Dispose();
                    Model = null;
                }
            }

            _isDisposed = true;
        }
        #endregion
    }
}
