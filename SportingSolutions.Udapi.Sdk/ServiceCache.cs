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
using System.Collections.Concurrent;
using System.Collections.Generic;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    /// <summary>
    /// This class is used to cache less volatile service data in order to avoid unnecessary API calls. 
    /// Cache is automatically invalidated at predefined interval (default every 60 seconds).
    /// </summary>
    internal sealed class ServiceCache
    {
        #region Constants

        private const int DefaultCacheInvalidationIntervalInSeconds = 60;

        #endregion

        #region Fields

        internal static bool Invalidate = true;

        private readonly ConcurrentDictionary<string, DateTime> _lastCacheInvalidation;
        private readonly ConcurrentStack<IFeature> _features;
        private readonly ConcurrentDictionary<string, List<IResource>> _resources;

        #endregion

        #region Properties

        internal static readonly ServiceCache Instance = new ServiceCache();

        internal bool IsEnabled { get; set; }

        internal int InvalidationInterval { get; set; }

        #endregion

        #region Constructors

        internal ServiceCache()
        {
            _features = new ConcurrentStack<IFeature>();
            _resources = new ConcurrentDictionary<string, List<IResource>>();
            _lastCacheInvalidation = new ConcurrentDictionary<string, DateTime>();
            IsEnabled = false;
            InvalidationInterval = DefaultCacheInvalidationIntervalInSeconds;
        }

        #endregion

        #region Public methods

        public IEnumerable<IFeature> GetCachedFeatures()
        {
            if (!IsEnabled)
            {
                return null;
            }

            var key = nameof(_features);
            if (ShouldInvalidateCache(key))
            {
                _features.Clear();
                _lastCacheInvalidation[key] = DateTime.Now;

                return null;
            }

            return _features;
        }

        public List<IResource> GetCachedResources(string sport)
        {
            if (!IsEnabled)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(sport))
            {
                throw new ArgumentException("Invalid sport name!", nameof(sport));
            }

            var key = string.Concat(sport, nameof(_resources));

            if (!_resources.ContainsKey(sport))
            {
                _resources[sport] = new List<IResource>();
            }

            if (ShouldInvalidateCache(key))
            {
                _resources[sport].Clear();
                _lastCacheInvalidation[key] = DateTime.Now;

                return null;
            }

            return _resources[sport];
        }

        public void CacheFeatures(IEnumerable<IFeature> features)
        {
            if (!IsEnabled)
            {
                return;
            }

            foreach (var feature in features)
            {
                _features.Push(feature);
            }
        }

        public void CacheResources(string sport, IEnumerable<IResource> resources)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sport))
            {
                throw new ArgumentException("Invalid sport name!", nameof(sport));
            }

            if (!_resources.ContainsKey(sport))
            {
                _resources[sport] = new List<IResource>();
            }

            _resources[sport].AddRange(resources);
        }

        #endregion

        #region Private methods

        private bool ShouldInvalidateCache(string key)
        {
            return !_lastCacheInvalidation.ContainsKey(key) ||
                   (DateTime.Now - _lastCacheInvalidation[key]).TotalSeconds >= InvalidationInterval;
        }

        #endregion
    }
}
