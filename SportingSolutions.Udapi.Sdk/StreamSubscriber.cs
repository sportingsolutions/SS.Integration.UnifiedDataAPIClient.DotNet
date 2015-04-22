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
using System.Threading;
using System.Threading.Tasks;
using log4net;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    /// <summary>
    ///     This consumer is associated with different queues, so it reads
    ///     from all of them and dispatch the message to the IDispatcher 
    ///     object passed into the constructor.
    /// 
    ///     As it is associated with many queues, these properties are
    ///     not valid:
    /// 
    ///     QueueingBasicConsuer.IsRunning
    ///     QueueingBasicConsumer.ConsumerTag
    /// 
    /// </summary>
    internal class StreamSubscriber : QueueingBasicConsumer, IDisposable
    {

        public event EventHandler<ShutdownEventArgs> SubscriberShutdown;

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));
        private readonly IDispatcher _dispatcher;
        private readonly CancellationTokenSource _cancellationHandle;
        private readonly ManualResetEvent _startBarrier;
        private readonly Task _consumer;
        private bool _modelShutdownRaised;

        public StreamSubscriber(IDispatcher dispatcher)
        {
            if(dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _cancellationHandle = new CancellationTokenSource();
            _dispatcher = dispatcher;
            _modelShutdownRaised = false;

            _startBarrier = new ManualResetEvent(false);
            _consumer = Task.Factory.StartNew(Consume);

            _logger.Debug("Streaming consumer created");
        }


        private void Consume()
        {
            _logger.Debug("Consumer thread is ready...");

            _startBarrier.WaitOne();

            _logger.Debug("Consumer thread is now reading from the streaming queue");

            bool run = true;

            while (!_cancellationHandle.IsCancellationRequested && run)
            {
                try
                {
                    var output = this.Queue.Dequeue();
                    if (output == null)
                        continue;


                    _dispatcher.DispatchMessage(output.ConsumerTag, Encoding.UTF8.GetString(output.Body));

                }
                catch (Exception ex)
                {
                    if (!_cancellationHandle.IsCancellationRequested)
                    {
                        _logger.Error("Error processing message from streaming queue", ex);

                        run = !this.Model.IsClosed;
                    }
                }
            }

            _logger.Debug("Consumer thread is quitting...");
        }

        public override void HandleBasicCancelOk(string consumerTag)
        {
            // this is raised when BasicCancel is called.
            // DO NOT call base.HandleBasicCancelOk() as it will in turn
            // call OnCancel()
            ConsumerCancelledEventHandler handler = m_consumerCancelled;
            if (handler != null)
                handler(this, new ConsumerEventArgs(consumerTag));
        }

        public override void HandleModelShutdown(IModel model, ShutdownEventArgs reason)
        {
            // this event is registred withing the model by the RabbitMQ library for each consumer...
            // moreover, if the connection goes down, the rabbitmq's IConnection implementation
            // raises an exception that bubbles down from IConnection to here 
            // (IConnection -> ISession -> IModel -> IConsumer)
            //
            // So, we handle this event ONCE for either handling any connection or model failure.

            _logger.InfoFormat("AMPQ Model is shutting down - eventRaised={0}", _modelShutdownRaised);
            if (SubscriberShutdown != null && !_modelShutdownRaised)
            {
                _modelShutdownRaised = true;
                SubscriberShutdown(this, reason);
            }
        }

        public void Start()
        {
            _startBarrier.Set();
        }

        public void Dispose()
        {
            _logger.Debug("Disposing main consumer");

            _modelShutdownRaised = true;
            _cancellationHandle.Cancel();
            _startBarrier.Set();

            try
            {
                Queue.Close();
            }
            catch { }

            Task.WaitAny(_consumer);
        }
    }
}
