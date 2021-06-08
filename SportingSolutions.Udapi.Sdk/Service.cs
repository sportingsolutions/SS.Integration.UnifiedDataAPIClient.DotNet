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

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Service : Endpoint, IService
    {
        #region Fields

        private static readonly object CacheSynchObjectLock = new object();

        #endregion

        #region Constructors

        internal Service(RestItem restItem, IConnectClient connectClient)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Service));
            Logger.DebugFormat("Instantiated service={0}", restItem.Name);
        }

        #endregion

        #region Implementation of IService

        public string Name => State.Name;

        public bool IsServiceCacheEnabled
        {
            get => ServiceCache.Instance.IsEnabled;
            set => ServiceCache.Instance.IsEnabled = value;
        }

        public int ServiceCacheInvalidationInterval
        {
            get => ServiceCache.Instance.InvalidationInterval;
            set => ServiceCache.Instance.InvalidationInterval = value;
        }

        public List<IFeature> GetFeatures()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.Append("Get all available features - ");

            try
            {
                var featuresList = GetFeaturesList(loggingStringBuilder);
                return featuresList;
            }
            finally
            {
                Logger.Debug(loggingStringBuilder);
            }
        }

        public IFeature GetFeature(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get feature={0} - ", name);

            try
            {
                var featuresList = GetFeaturesList(loggingStringBuilder);
                var feature = featuresList?.FirstOrDefault(f => f.Name.Equals(name));
                return feature;
            }
            finally
            {
                Logger.Debug(loggingStringBuilder);
            }
        }

        #endregion

        #region Private methods

        private IEnumerable<RestItem> GetFeaturesListFromApi(StringBuilder loggingStringBuilder)
        {
            loggingStringBuilder.Append("Getting features list from API - ");

            return FindRelationAndFollow(
                "http://api.sportingsolutions.com/rels/features/list",
                "GetFeature Http error",
                loggingStringBuilder);
        }

        private List<IFeature> GetFeaturesList(StringBuilder loggingStringBuilder)
        {
            if (ServiceCache.Instance.IsEnabled)
            {
                var featuresList = ServiceCache.Instance.GetCachedFeatures();

                //if featuresList is null it means it hasn't been cached yet or has been evicted
                if (featuresList == null)
                {
                    lock (CacheSynchObjectLock)
                    {
                        featuresList = ServiceCache.Instance.GetCachedFeatures();

                        //if featuresList is null it means it hasn't been cached yet or has been evicted
                        if (featuresList == null)
                        {
                            loggingStringBuilder.AppendLine(
                                "features cache is empty - going to retrieve the list of features from the API now - ");
                            var resources = GetFeaturesListFromApi(loggingStringBuilder);
                            if (resources == null)
                            {
                                Logger.Warn($"Return Method=GetFeaturesListFromApi is NULL");
                                return null;
                            }
                            ServiceCache.Instance.CacheFeatures(
                                featuresList = resources
                                    .Select(restItem => new Feature(restItem, ConnectClient))
                                    .Cast<IFeature>()
                                    .ToList());

                            loggingStringBuilder.AppendLine(
                                $"list with {featuresList.Count} features has been cached - ");
                        }
                        else
                        {
                            loggingStringBuilder.AppendLine($"retrieved {featuresList.Count} features from cache - ");
                        }
                    }
                }
                else
                {
                    loggingStringBuilder.AppendLine($"retrieved {featuresList.Count} features from cache - ");
                }

                return featuresList;
            }


            var resource = GetFeaturesListFromApi(loggingStringBuilder);
            if (resource == null)
            {
                Logger.Warn($"Return Method=GetFeaturesListFromApi is NULL");
                return null;
            }

            return
                resource
                    .Select(restItem => new Feature(restItem, ConnectClient))
                    .Cast<IFeature>()
                    .ToList();
        }

        #endregion
    }
}
