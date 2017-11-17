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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Feature : Endpoint, IFeature
    {
        #region Fields

        private static readonly object CacheSynchObjectLock = new object();

        #endregion

        #region Constructors

        internal Feature(RestItem restItem, IConnectClient connectClient)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Feature).ToString());
            Logger.DebugFormat("Instantiated feature={0}", restItem.Name);
        }

        #endregion

        #region Implementation of IFeature

        public string Name => State.Name;

        public List<IResource> GetResources()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.Append($"Get all available resources for feature={Name} - ");

            try
            {
                var resourcesList = GetResourcesList(Name, loggingStringBuilder);
                return resourcesList;
            }
            finally
            {
                Logger.Debug(loggingStringBuilder);
            }
        }

        public IResource GetResource(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat($"Get resource={name} for feature={Name} - ");

            try
            {
                var resourcesList = GetResourcesList(Name, loggingStringBuilder);
                var resource = resourcesList.FirstOrDefault(r => r.Name.Equals(name));
                return resource;
            }
            finally
            {
                Logger.Debug(loggingStringBuilder);
            }
        }

        #endregion

        #region Private methods

        private IEnumerable<RestItem> GetResourcesListFromApi(StringBuilder loggingStringBuilder)
        {
            loggingStringBuilder.Append("Getting resources list from API - ");

            return FindRelationAndFollow(
                "http://api.sportingsolutions.com/rels/resources/list",
                "GetResources HTTP error",
                loggingStringBuilder);
        }

        private List<IResource> GetResourcesList(string sport, StringBuilder loggingStringBuilder)
        {
            if (ServiceCache.Instance.IsEnabled)
            {
                var resourcesList = ServiceCache.Instance.GetCachedResources(sport);

                //if resourcesList is null it means it hasn't been cached yet or has been evicted
                if (resourcesList == null)
                {
                    lock (CacheSynchObjectLock)
                    {
                        resourcesList = ServiceCache.Instance.GetCachedResources(sport);

                        //if resourcesList is null it means it hasn't been cached yet or has been evicted
                        if (resourcesList == null)
                        {
                            loggingStringBuilder.AppendLine(
                                $"resources cache is empty for feature={Name} - going to retrieve list of resources from the API now - ");

                            ServiceCache.Instance.CacheResources(
                                sport,
                                resourcesList = GetResourcesListFromApi(loggingStringBuilder)
                                    .Select(restItem => new Resource(restItem, ConnectClient))
                                    .Cast<IResource>()
                                    .ToList());

                            loggingStringBuilder.AppendLine(
                                $"list with {resourcesList.Count} resources has been cached for feature={Name} - ");
                        }
                        else
                        {
                            loggingStringBuilder.AppendLine(
                                $"retrieved {resourcesList.Count} resources from cache for feature={Name} - ");
                        }
                    }
                }
                else
                {
                    loggingStringBuilder.AppendLine(
                        $"retrieved {resourcesList.Count} resources from cache for feature={Name} - ");
                }

                return resourcesList;
            }

            return
                GetResourcesListFromApi(loggingStringBuilder)
                    .Select(restItem => new Resource(restItem, ConnectClient))
                    .Cast<IResource>()
                    .ToList();
        }

        #endregion
    }
}
