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
    public class Feature : Endpoint, IFeature
    {
        #region Constructors

        internal Feature(RestItem restItem, IConnectClient connectClient)
            : base(restItem, connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Feature));
            Logger.DebugFormat("Instantiated feature={0}", restItem.Name);
        }

        #endregion

        #region Implementation of IFeature

        public string Name => State.Name;

        public List<IResource> GetResources()
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get all available resources from feature={0} - ", Name);

            var resourcesList = ServiceCache.Instance.GetCachedResources(Name);

            if (resourcesList == null)
            {
                resourcesList =
                    FindRelationAndFollow(
                            "http://api.sportingsolutions.com/rels/resources/list",
                            "GetResources HTTP error",
                            loggingStringBuilder)
                        .Select(restItem => new Resource(restItem, ConnectClient))
                        .Cast<IResource>()
                        .ToList();
                ServiceCache.Instance.CacheResources(Name, resourcesList);
            }
            Logger.Debug(loggingStringBuilder);
            return resourcesList;
        }

        public IResource GetResource(string name)
        {
            var loggingStringBuilder = new StringBuilder();
            loggingStringBuilder.AppendFormat("Get resource={0} from feature={1} - ", name, Name);

            var resourcesList = ServiceCache.Instance.GetCachedResources(Name);
            var resource = resourcesList?.FirstOrDefault(r => r.Name.Equals(name));
            if (resource == null)
            {
                var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/resources/list", "GetResource HTTP error", loggingStringBuilder);
                resource =
                (
                    from restItem in restItems
                    where restItem.Name == name
                    select new Resource(restItem, ConnectClient)
                ).FirstOrDefault();
                ServiceCache.Instance.CacheResources(Name, new[] { resource });
            }

            Logger.Debug(loggingStringBuilder);
            return resource;
        }

        #endregion
    }
}
