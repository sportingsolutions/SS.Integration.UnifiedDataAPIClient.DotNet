using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Configuration;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    internal class FixtureController : IFixtureController
    {
        private static readonly ConcurrentDictionary<string, FixtureControlItem<StreamListener>> _fixtureTasks = new ConcurrentDictionary<string, FixtureControlItem<StreamListener>>();
        private volatile static object _locker = new object();
        private static readonly ILog _logger = LogManager.GetLogger(typeof(FixtureController));

        private volatile static object _lockAddItem = new object();

        private static bool _isAddingItems;
        public static bool IsAddingItems
        {
            get { return _isAddingItems; }
            private set { _isAddingItems = value; }
        }

        public static void AddListener(string id, Func<StreamListener> createListener)
        {
            var fixtureControlItem = new FixtureControlItem<StreamListener>(id, createListener, FixtureStatus.Active);
            lock (_lockAddItem)
            {
                if (!_fixtureTasks.ContainsKey(id))
                    _fixtureTasks[id] = fixtureControlItem;
                else
                {
                    _logger.InfoFormat("Disabling duplicated streaming listener for fixtureId={0}",id);
                    fixtureControlItem.Item.StopListening();
                    _logger.InfoFormat("Disabled duplicated streaming listener for fixtureId={0}", id);
                }
            }
        }

        /// <summary>
        /// Blocks all functions apart from AddListener 
        /// </summary>
        public static void BeginAddingItems()
        {
            _logger.DebugFormat("Adding/Checking fixtures in progress...");
            //Monitor.Enter(_locker);
            _isAddingItems = true;
        }

        public static void FinishAddingItems()
        {
            _isAddingItems = false;
            //Monitor.Exit(_locker);
            _logger.DebugFormat("Fixture addding/checking should be complete.");
        }

        public static FixtureStatus GetStatus(string id)
        {
            lock (_locker)
            {
                return _fixtureTasks[id].FixtureStatus;
            }
        }

        public static DateTime GetLastMessageReceived(string id)
        {
            lock(_locker)
            {
                return _fixtureTasks[id].Item.StreamStatistics.LastMessageReceived;
            }
        }

        private static FixtureControlItem<StreamListener> GetObjectById(string id)
        {
            lock (_locker)
            {
                if (!_fixtureTasks.ContainsKey(id))
                    throw new KeyNotFoundException(string.Format("Item with id={0} does not exist", id));

                return _fixtureTasks[id];
            }
        }

        public static void RemoveFixture(string id)
        {
            lock (_locker)
            {
                if (!Contains(id)) return;
                FixtureControlItem<StreamListener> item = null;
                _fixtureTasks.TryRemove(id,out item);
                _logger.InfoFormat("Removed fixtureId={0} from controller", id);
            }
        }

        void IFixtureController.RemoveFixture(string id)
        {
            RemoveFixture(id);
        }

        public static void RestartFixture(string id)
        {
            lock (_locker)
            {
                if (!_fixtureTasks.ContainsKey(id))
                {
                    _logger.WarnFormat("Ignoring RestartFixture request for fixtureId={0}, fixture NOT FOUND", id);
                    return;
                }

                _logger.InfoFormat("Restarting fixtureId={0}", id);
                
                var itemController = GetObjectById(id);
                if(itemController.FixtureStatus != FixtureStatus.Stopped)
                    StopFixture(id);

                // create new listener 
                itemController = itemController.ReCreate();
                _fixtureTasks[id] = itemController;
             
                _logger.InfoFormat("Restarted fixtureId={0}", id);
            }
        }

        public static IEnumerable<StreamListener> GetAll()
        {
            return _fixtureTasks.Values.Select(x => x.Item);
        }

        void IFixtureController.StopFixture(string id)
        {
            StopFixture(id);
        }

        bool IFixtureController.Contains(string id)
        {
            return Contains(id);
        }

        public static bool Contains(string id)
        {
            return _fixtureTasks.ContainsKey(id);
        }

        public static void DoAll(Action<FixtureControlItem<StreamListener>> action, int maxThreads = 10)
        {
            lock (_locker)
            {
                Parallel.ForEach(_fixtureTasks.Values,
                                 new ParallelOptions() { MaxDegreeOfParallelism = maxThreads }, action);
            }
        }

        void IFixtureController.RestartFixture(string id)
        {
            RestartFixture(id);
        }

        public static void StopFixture(string id, bool block = false)
        {
            lock (_locker)
            {
                if (!_fixtureTasks.ContainsKey(id))
                {
                    _logger.WarnFormat("Ignoring StopFixture request for fixtureId={0}, fixture NOT FOUND",id);
                    return;
                }

                var logMessage = block ? "Block" : "Stopp";

                _logger.InfoFormat("{0}ing fixtureId={1}", logMessage, id);
                // supspend all markets
                var listener = GetObjectById(id);
                listener.Item.StopListening();
                
                listener.FixtureStatus = (block) ? FixtureStatus.Blocked : FixtureStatus.Stopped;
                _logger.InfoFormat("{0}ed fixtureId={1}", logMessage, id);
            }
        }

        public static StreamListener GetItem(string id)
        {
            lock (_locker)
            {
                return _fixtureTasks[id].Item;
            }
        }
    }
}
