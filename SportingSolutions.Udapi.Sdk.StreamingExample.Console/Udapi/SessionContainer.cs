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
using SportingSolutions.Udapi.Sdk.Interfaces;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Udapi
{
    public class SessionContainer
    {
        private static volatile ISession _theSession;
        private static readonly object _lock = new object();

        private readonly ILog _logger = LogManager.GetLogger(typeof (SessionContainer).ToString());
        private readonly ICredentials _credentials;
        private readonly Uri _url;

        public SessionContainer(ICredentials credentials, Uri url)
        {
            _credentials = credentials;
            _url = url;
        }

        public ISession Session
        {
            get
            {
                if (_theSession == null)
                {
                    lock (_lock)
                    {
                        if (_theSession == null)
                        {
                            _logger.Info("Connecting to UDAPI....");
                            _theSession = SessionFactory.CreateSession(_url, _credentials, false);
                            _logger.Info("Successfully connected to UDAPI.");
                        }
                    }
                }

                return _theSession;
            }
        }

        public void ReleaseSession()
        {
            lock (_lock)
            {
                _theSession = null;
            }
        }
    }
}
