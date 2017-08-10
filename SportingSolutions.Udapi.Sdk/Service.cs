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
using System.Text;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Service : Endpoint, IService
    {
        #region Constructors

        internal Service(RestItem restItem, IConnectClient connectClient)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Service).ToString());
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

            var featuresList = ServiceCache.Instance.GetCachedFeatures();
            if (featuresList == null)
            {
                featuresList =
                    FindRelationAndFollow(
                            "http://api.sportingsolutions.com/rels/features/list",
                            "GetFeatures Http error",
                            loggingStringBuilder)
                        .Select(restItem => new Feature(restItem, ConnectClient))
                        .Cast<IFeature>()
                        .ToList();
                ServiceCache.Instance.CacheFeatures(featuresList);
            }
            Logger.Debug(loggingStringBuilder);
            return featuresList.ToList();
        }

        public IFeature GetFeature(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get feature={0} - ", name);

            var featuresList = ServiceCache.Instance.GetCachedFeatures();
            var feature = featuresList?.FirstOrDefault(f => f.Name.Equals(name));
            if (feature == null)
            {
                var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/features/list", "GetFeature Http error", loggingStringBuilder);
                feature =
                (
                    from restItem in restItems
                    where restItem.Name == name
                    select new Feature(restItem, ConnectClient)
                ).FirstOrDefault();
                ServiceCache.Instance.CacheFeatures(new[] { feature });
            }

            Logger.Debug(loggingStringBuilder);
            return feature;
        }

        #endregion
    }
}
