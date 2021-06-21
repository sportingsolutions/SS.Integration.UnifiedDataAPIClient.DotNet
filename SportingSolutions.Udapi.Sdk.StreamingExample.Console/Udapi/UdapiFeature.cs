﻿//Copyright 2012 Spin Services Limited

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
    internal class UdapiFeature : BaseSS<IFeature>, IFeature
    {
        private readonly String _featureName;
        
        internal UdapiFeature(String featureName, IFeature theFeature)
        {
            _logger = LogManager.GetLogger(typeof(UdapiFeature));
            
            _featureName = featureName;
            //Assign the method that is needed to get a fresh instance of the real feature
            TheReconnectMethod = InitUdapiFeature;
            _theRealObject = theFeature;
        }

        private void InitUdapiFeature()
        {
            _logger.Debug("UDAPI, Getting Service");
            var realService = Session.GetService("UnifiedDataAPI");
            _logger.Debug("UDAPI, Retrieved Service");
            _logger.DebugFormat("UDAPI, Getting Feature {0}", _featureName);
            _theRealObject = realService.GetFeature(_featureName);
            _logger.DebugFormat("UDAPI, Retrieved Feature {0}", _featureName);
        }

        public List<IResource> GetResources()
        {
            try
            {
                var realResources = ReconnectOnException(x => x.GetResources(), _theRealObject);

                return realResources != null ? realResources.Select(resource => new UdapiResource(_featureName, resource.Name, resource)).Cast<IResource>().ToList() : new List<IResource>();
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} - Unable to retrieve fixtures from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.", _featureName);
                throw;
            }
            
        }

        public IResource GetResource(string name)
        {
            try
            {
                var realResource = ReconnectOnException(x => x.GetResource(name), _theRealObject);
                if (realResource != null)
                {
                    return new UdapiResource(_featureName, name, realResource);
                }
                return null;
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} : {1} - Unable to retrieve fixture from GTP-UDAPI after multiple attempts. Check the Evenue adpater is running ok.",_featureName, name);
                throw;
            }
        }

        public string Name
        {
            get { return ReconnectOnException(x => x.Name, _theRealObject); }
        }
    }
}
