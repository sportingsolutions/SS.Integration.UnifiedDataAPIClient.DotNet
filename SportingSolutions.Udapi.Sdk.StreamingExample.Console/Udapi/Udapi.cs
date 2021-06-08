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
using System.Collections.Generic;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Interfaces;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Udapi
{
    internal class Udapi : BaseSS<IService>, IService
    {
        internal Udapi()
        {
            _logger = LogManager.GetLogger(typeof(Udapi));
            
            try
            {
                //Assign the method that is needed to get a fresh instance of the real service
                TheReconnectMethod = InitUdapiService;
                Init(true);
            }
            catch (Exception)
            {
                _logger.Error("Unable to connect to the GTP-UDAPI. Check the Evenue adapter is running ok.");
                throw;
            }
        }

        private void InitUdapiService()
        {
            _logger.Debug("UDAPI, Getting Service");
            _theRealObject = Session.GetService("UnifiedDataAPI");
            _logger.Debug("UDAPI, Retrieved Service");
        }

        public List<IFeature> GetFeatures()
        {
            try
            {
                var realFeatures = ReconnectOnException(x => x.GetFeatures(), _theRealObject);
                return realFeatures != null ? realFeatures.Select(feature => new UdapiFeature(feature.Name, feature)).Cast<IFeature>().ToList() : new List<IFeature>();
            }
            catch (Exception)
            {
                _logger.Error("Unable to retrieve sports from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.");
                throw;
            }
        }

        public IFeature GetFeature(string name)
        {
            try
            {
                var realFeature = ReconnectOnException(x => x.GetFeature(name), _theRealObject);
                return realFeature != null ? new UdapiFeature(name, realFeature) : null;
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} - Unable to retrieve sport from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.",name);
                throw;
            }
        }

        public string Name
        {
            get { return ReconnectOnException(x => x.Name, _theRealObject); }
        }

        public bool IsServiceCacheEnabled { get; set; } = false;

        public int ServiceCacheInvalidationInterval { get; set; }
    }
}
