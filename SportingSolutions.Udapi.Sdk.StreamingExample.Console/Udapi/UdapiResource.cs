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
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Udapi
{
    internal class UdapiResource : BaseSS<IResource>, IResource
    {
        private readonly String _featureName;
        private readonly String _resourceName;

        private readonly ILog _simpleLogger;

        internal UdapiResource(String featureName, String resourceName, IResource theResource)
        {
            _logger = LogManager.GetLogger(typeof(UdapiResource).ToString());
            _simpleLogger = LogManager.GetLogger("SimpleUDAPILogger");
            _featureName = featureName;
            _resourceName = resourceName;
            //Assign the method that is needed to get a fresh instance of the real resource
            TheReconnectMethod = InitUdapiResource;
            _theRealObject = theResource;
        }

        private void InitUdapiResource()
        {
            _logger.Debug("UDAPI, Getting Service");
            var realService = Session.GetService("UnifiedDataAPI");
            _logger.Debug("UDAPI, Retrieved Service");
            _logger.DebugFormat("UDAPI, Getting Feature {0}", _featureName);
            var realFeature = realService.GetFeature(_featureName);
            _logger.DebugFormat("UDAPI, Retrieved Feature {0}", _featureName);
            _logger.DebugFormat("UDAPI, Getting Resource {0}", _resourceName);
            _theRealObject = realFeature.GetResource(_resourceName);
            _logger.DebugFormat("UDAPI, Retrieved Resource {0}", _resourceName);
        }

        public string GetSnapshot()
        {
            try
            {
                var snapshot = ReconnectOnException(x => x.GetSnapshot(), _theRealObject);
                _logger.DebugFormat("Snapshot - {0}", _theRealObject);
                return snapshot;
            }
            catch (Exception)
            {
                _simpleLogger.ErrorFormat("{0} : {1} - Unable to retrieve Snapshot from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.", _featureName, _resourceName);
                throw;
            }
        }

        public void StartStreaming()
        {
            try
            {
                ReconnectOnException(x => x.StartStreaming(), _theRealObject);
            }
            catch (Exception)
            {
                _simpleLogger.ErrorFormat("{0} : {1} - Unable to start streaming from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.", _featureName, _resourceName);
                throw;
            }
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            try
            {
                ReconnectOnException(x => x.StartStreaming(echoInterval, echoMaxDelay), _theRealObject);
            }
            catch (Exception)
            {
                _simpleLogger.ErrorFormat("{0} : {1} - Unable to start streaming from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.", _featureName, _resourceName);
                throw;
            }
        }

        public void PauseStreaming()
        {
            //do nothing this method is deprectaed
        }

        public void UnPauseStreaming()
        {
            //do nothing this method is deprectaed
        }

        public void StopStreaming()
        {
            try
            {
                ReconnectOnException(x => x.StopStreaming(), _theRealObject);
            }
            catch (Exception)
            {
                _simpleLogger.ErrorFormat("{0} : {1} - Unable to stop streaming from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.", _featureName, _resourceName);
                throw;
            }
        }

        public string Id
        {
            get { return ReconnectOnException(x => x.Id, _theRealObject); }
        }

        public string Name
        {
            get { return ReconnectOnException(x => x.Name, _theRealObject); }
        }

        public bool IsDisposed
        {
            get
            {
                return ReconnectOnException(x => x.IsDisposed, _theRealObject);
            }
        }

        public Summary Content
        {
            get { return ReconnectOnException(x => x.Content, _theRealObject); }
        }

        public event EventHandler StreamConnected
        {
            add
            {
                if (_theRealObject != null)
                {
                    _theRealObject.StreamConnected += value;
                }
            }
            remove
            {
                if (_theRealObject != null)
                {
                    _theRealObject.StreamConnected -= value;
                }
            }
        }

        public event EventHandler StreamDisconnected
        {
            add
            {
                if (_theRealObject != null)
                {
                    _theRealObject.StreamDisconnected += value;
                }
            }
            remove
            {
                if (_theRealObject != null)
                {
                    _theRealObject.StreamDisconnected -= value;
                }
            }
        }

        public event EventHandler<StreamEventArgs> StreamEvent
        {
            add
            {
                if (_theRealObject != null)
                {
                    _theRealObject.StreamEvent += value;
                }
            }
            remove
            {
                if (_theRealObject != null)
                {
                    _theRealObject.StreamEvent -= value;
                }
            }
        }
    }
}
