//Copyright 2017 Spin Services Limited

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
using System.Collections.Specialized;
using System.Runtime.Caching;
using log4net;
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

        private const int DefaultCacheInvalidationIntervalInSeconds = 58;
        private const string FeaturesCacheKey = "Features";
        private const string ResourcesCacheKeyTemplate = "{0}_Resources";

        #endregion

        #region Fields

        private readonly MemoryCache _memoryCache;
        private readonly CacheItemPolicy _defaultCacheItemPolicy;

        #endregion

        #region Properties

        internal static readonly ServiceCache Instance = new ServiceCache();

        internal bool IsEnabled { get; set; }

        internal int InvalidationInterval { get; set; }

        #endregion

        #region Constructors

        internal ServiceCache()
        {
            var defaultCacheConfig = new NameValueCollection
            {
                //memory cache can grow to a maximum of 1.5GB per entity
                //for both Features and Resources can go up to 3GB 
                //(should't happen but for safety if it goes there it will automatically remove cached items)
                {"cacheMemoryLimitMegabytes", "1536"},
                //interval for cache to check the memory load
                {"pollingInterval", "00:01:00"}
            };

            IsEnabled = false;
            InvalidationInterval = DefaultCacheInvalidationIntervalInSeconds;

            _memoryCache = new MemoryCache(nameof(ServiceCache), defaultCacheConfig);

            _defaultCacheItemPolicy = new CacheItemPolicy();
        }

        #endregion

        #region Public methods

        public List<IFeature> GetCachedFeatures()
        {
            if (!IsEnabled)
            {
                return null;
            }

            return (List<IFeature>)_memoryCache.Get(FeaturesCacheKey);
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

            return (List<IResource>)_memoryCache.Get(GetResourceCacheKey(sport));
        }

        public void CacheFeatures(List<IFeature> features)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            //the cached item gets automatically evicted after InvalidationInterval
            _defaultCacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(InvalidationInterval);

            _memoryCache.Set(FeaturesCacheKey, features, _defaultCacheItemPolicy);
        }

        public void CacheResources(string sport, List<IResource> resources)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sport))
            {
                throw new ArgumentException("Invalid sport name!", nameof(sport));
            }
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            //the cached item gets automatically evicted after InvalidationInterval
            _defaultCacheItemPolicy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(InvalidationInterval);

            _memoryCache.Set(GetResourceCacheKey(sport), resources, _defaultCacheItemPolicy);
        }

        #endregion

        #region Private methods

        private string GetResourceCacheKey(string sport)
        {
            return string.Format(ResourcesCacheKeyTemplate, sport);
        }

        #endregion
    }
}
