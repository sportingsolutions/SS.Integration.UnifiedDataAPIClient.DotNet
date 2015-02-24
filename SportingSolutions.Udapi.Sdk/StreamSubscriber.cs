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
    internal class StreamSubscriber : QueueingBasicConsumer, IDisposable
    {

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamSubscriber));
        private readonly IDispatcher _dispatcher;
        private readonly CancellationTokenSource _cancellationHandle;
        private readonly ManualResetEvent _startBarrier;
        private readonly Task _consumer;

        public StreamSubscriber(IDispatcher dispatcher)
        {
            _cancellationHandle = new CancellationTokenSource();
            _dispatcher = dispatcher;
            _startBarrier = new ManualResetEvent(false);
            _consumer = Task.Factory.StartNew(Consume);

            _logger.Debug("Consumer created");
        }


        private void Consume()
        {
            _logger.Debug("Consumer thread is ready...");

            _startBarrier.WaitOne();

            _logger.Debug("Consumer thread is reading from the streaming queue");

            while (!_cancellationHandle.IsCancellationRequested)
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
                    }
                }
            }

            _logger.Debug("Consumer thread is quitting...");
        }

        public override void HandleBasicCancelOk(string consumerTag)
        {
            ConsumerCancelledEventHandler handler = m_consumerCancelled;
            if (handler != null)
                handler(this, new ConsumerEventArgs(consumerTag));
        }

        public void Start()
        {
            _startBarrier.Set();
        }

        public void Dispose()
        {
            _logger.Debug("Disposing main consumer");

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
