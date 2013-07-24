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
using System.Collections.Specialized;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class Service : Endpoint, IService
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(Service).ToString());

        internal Service(NameValueCollection headers, RestItem restItem):base(headers, restItem)
        {
            StreamSubscriber.State =
                FindRelationAndFollow("http://api.sportingsolutions.com/rels/features/list")
                    .First(item => item.Name == "Echo");
            _logger.DebugFormat("Instantiated Service {0}",restItem.Name);
        }

        public string Name
        {
            get { return State.Name; }
        }



        public List<IFeature> GetFeatures()
        {
            _logger.InfoFormat("Get all available features from {0}",Name);
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/features/list");

            return restItems.Select(restItem => new Feature(Headers, restItem)).Cast<IFeature>().ToList();
        }

        public IFeature GetFeature(string name)
        {
            _logger.InfoFormat("Get {0} from {1}",name,Name);
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/features/list");
            return (from restItem in restItems where restItem.Name == name select new Feature(Headers, restItem)).FirstOrDefault();
        }
    }
}
