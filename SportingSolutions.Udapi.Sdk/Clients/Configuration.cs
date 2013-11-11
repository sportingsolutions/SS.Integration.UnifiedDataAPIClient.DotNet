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
        private readonly Uri _baseUrl;
        private readonly string _contentType;
        private readonly int _timeout;
        private readonly bool _compression;

        public Configuration(Uri baseUrl)
        {
            if (baseUrl == null) throw new ArgumentNullException("baseUrl");

            _baseUrl = baseUrl;
            _contentType = "application/json";
            _timeout = 60000;
            _compression = true;
        }

        public Configuration(Uri baseUrl, string contentType, int timeout, bool compression)
        {
            if (string.IsNullOrEmpty(contentType)) throw new ArgumentNullException("contentType");
            if (baseUrl == null) throw new ArgumentNullException("baseUrl");

            _baseUrl = baseUrl;
            _contentType = contentType;
            _timeout = timeout;
            _compression = compression;
        }

        public Uri BaseUrl { get { return _baseUrl; } }
        public string ContentType { get { return _contentType; } }
        public int Timeout { get { return _timeout; } }
        public bool Compression { get { return _compression; } }
    }
}
