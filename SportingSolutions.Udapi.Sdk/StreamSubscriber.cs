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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Akka.Actor;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model.Message;


namespace SportingSolutions.Udapi.Sdk
{

    internal class StreamSubscriber : DefaultBasicConsumer, IStreamSubscriber, IDisposable
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));

	    public IConsumer Consumer { get; }

	    private bool _isDisposed;
	    private bool _isConsuming;
	    private string _queue;

		internal bool IsStreamingStopped => Model == null || !Model.IsOpen || !_isConsuming;

        internal bool IsDisposed => _isDisposed;

        public StreamSubscriber(IModel model, /*IConsumer consumer,*/ IActorRef dispatcher)
            : base(model)
        {
	        
	        ConsumerTag = "SingleQueue";
	        Dispatcher = dispatcher;
            _isDisposed = false;
		}



	    public bool ResumeConsuming(string queue)
	    {
		    _logger.Info($"ResumeConsuming trigered for queue={queue}");

		    if (queue == null)
			    return false;
		    try
		    {
			    QueueBind(Model, queue);
			    try
			    {
				    Model.BasicConsume(queue, true, /*Consumer.Id*/ ConsumerTag, this);
				    _isConsuming = true;
				    _queue = queue;
					    
			    }
			    catch (Exception e)
			    {
				    _logger.Warn("Failed ReConsuming  Queue ", e);
				    return false;
			    }

		    }
		    catch (Exception e)
		    {
			    _logger.Warn("Failed ReBind to Queue ", e);
			    return false;
		    }
		    _logger.Info($"ResumeConsuming executed for queue={queue}");
		    Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });
			return true;
		}


		public string StartConsuming()
	    {
		    _logger.Info($"StartConsuming trigered");

			_queue = CreateNewQueue(Model);

			try
		    {
			    Model.BasicConsume(_queue, true, /*Consumer.Id*/ ConsumerTag, this);
			    _isConsuming = true;

			}
		    catch (Exception e)
		    {
			    _logger.Error("Error Consuming  Queue ", e);
			    throw;
		    }

		    Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });
			return _queue;


	    }


		private void QueueBind(IModel model, string queue)
	    {
		    model.QueueBind(queue, "priceupdates", "priceupdates.#", null);
		    model.QueueBind(queue, "priceupdates", "echo.#", null);
		}

	    private string CreateNewQueue(IModel model)
	    {
			
		    var newQueue = Guid.NewGuid().ToString();
		    _logger.Info($"Creating queue with queueId={newQueue} for channelNumber={model.ChannelNumber}");

		    var args = new Dictionary<string, object>()
		    {
			    {"x-max-length", 100},
			    {"x-message-ttl", 30000},
			    {"x-expires", 10000}
		    };
		    try
		    {
			    model.QueueDeclare(newQueue, false, false, false, args);
			}
		    catch (Exception e)
		    {
				_logger.Error($"Error creating queue {newQueue} ", e);
				throw;
		    }

		    try
		    {
			    QueueBind(model, newQueue);
		    }
		    catch (Exception e)
		    {
			    _logger.Error($"Error binding to the  queue={newQueue} ", e);
			    throw;
		    }

			return newQueue;
	    }

        public void StopConsuming()
        {
			if (!_isConsuming)
				return;


	        _logger.Info($"StopConsuming trigered for {_queue}");

			try
            {
                if (!IsStreamingStopped )
                {
                    Model.BasicCancel(_queue);
                }

	            _isConsuming = false;

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

				try
				{
					Dispose();
				}
				catch { }

				_logger.DebugFormat($"Streaming stopped for queue={_queue}");
            }
        }

        //public IConsumer Consumer { get; }

        public IActorRef Dispatcher { get; }

        #region DefaultBasicConsumer

        public override void HandleBasicConsumeOk(string consumerTag)
        {
            _logger.Debug($"HandleBasicConsumeOk consumerTag={consumerTag ?? "null"}");
            //Dispatcher.Tell(new NewSubscriberMessage { Subscriber = this });

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

            Dispatcher.Tell(new StreamUpdateMessage { Id = routingKey.ExtractIdFromRoutingKey(), Message = Encoding.UTF8.GetString(body), ReceivedAt = DateTime.UtcNow});
        }

        public override void HandleBasicCancel(string consumerTag)
        {
            _logger.Debug($"HandleBasicCancel consumerTag={consumerTag ?? "null"}");
            base.HandleBasicCancel(consumerTag);
            Dispatcher.Tell(new RemoveSubscriberMessage { Subscriber = this });
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
