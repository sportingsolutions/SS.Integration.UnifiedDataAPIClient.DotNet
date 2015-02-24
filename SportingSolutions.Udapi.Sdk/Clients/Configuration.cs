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

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public class Configuration : IConfiguration
    {
        private const string DEFAULT_CONTENT_TYPE = "application/json";
      
        public Configuration(Uri baseUrl, int timeoutMilliseconds = 60000)
        {
            if (baseUrl == null) 
                throw new ArgumentNullException("baseUrl");

            Compression = true;
            BaseUrl = baseUrl;
            Timeout = timeoutMilliseconds;
            ContentType = DEFAULT_CONTENT_TYPE;
        }

        public Configuration(Uri baseUrl, string contentType, int timeoutMilliseconds, bool compressionEnabled)
            : this(baseUrl, timeoutMilliseconds)
        {
            if (string.IsNullOrEmpty(contentType)) throw new ArgumentNullException("contentType");
            
            ContentType = contentType;
            Compression = compressionEnabled;
        }

        public int Timeout { get; set; }

        public Uri BaseUrl { get; private set; }

        public string ContentType { get; set; }

        public bool Compression { get; set; }
    }
}
