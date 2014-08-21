using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace SportingSolutions.Udapi.Sdk
{
    public class BlockingQueue<T> : IEnumerable
    {
        protected Queue<T> _queue = new Queue<T>();

        protected bool _isOpen = true;

        public void Enqueue(T item)
        {
            lock (_queue)
            {
                EnsureIsOpen();
                _queue.Enqueue(item);
                Monitor.Pulse(_queue);
            }
        }

        public T Dequeue()
        {
            lock (_queue)
            {
                while (_queue.Count == 0)
                {
                    EnsureIsOpen();
                    Monitor.Wait(_queue);
                }
                return _queue.Dequeue();
            }
        }

        public bool Dequeue(int millisecondsTimeout, out T result)
        {
            if (millisecondsTimeout == -1)
            {
                result = Dequeue();
                return true;
            }
            
            var now = DateTime.Now;
            lock (_queue)
            {
                while (_queue.Count == 0)
                {
                    EnsureIsOpen();
                    var msTaken = (int) (DateTime.Now - now).TotalMilliseconds;
                    var msRemaining = millisecondsTimeout - msTaken;
                    if (msRemaining <= 0)
                    {
                        result = default(T);
                        return false;
                    }
                    Monitor.Wait(_queue, msRemaining);
                }
                result = _queue.Dequeue();
                return true;
            }
        }


        private void EnsureIsOpen()
        {
            if (!_isOpen)
            {
                throw new Exception();
            }
        }

        public void Close()
        {
            lock (_queue)
            {
                _isOpen = false;
                Monitor.PulseAll(_queue);
            }
        }

        public IEnumerator GetEnumerator()
        {
            return new BlockingQueueEnumerator<T>(this);
        }
    }

    public class BlockingQueueEnumerator<T> : IEnumerator<T>
    {
        protected BlockingQueue<T> _queue;
        protected T _current;

        public BlockingQueueEnumerator(BlockingQueue<T> queue)
        {
            _queue = queue;
        }

        public bool MoveNext()
        {
            try
            {
                _current = _queue.Dequeue();
                return true;
            }
            catch (Exception)
            {
                _current = default(T);
                return false;
            }
        }

        void IEnumerator.Reset()
        {
            throw new NotImplementedException();
        }

        void IDisposable.Dispose() { }

        public T Current
        {
            get { return _current; }
        }

        object IEnumerator.Current
        {
            get
            {
                if (_current == null)
                {
                    throw new InvalidOperationException();
                }
                
                return _current;
            }
        }
    }
}
