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
    public class Credentials : ICredentials
    {
        private readonly string _apiUser;
        private readonly string _apiKey;

        public Credentials(string apiUser, string apiKey)
        {
            if (string.IsNullOrEmpty(apiUser)) throw new ArgumentNullException("apiUser");
            if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException("apiKey");

            _apiUser = apiUser;
            _apiKey = apiKey;
        }

        public string ApiUser { get { return _apiUser; } }
        public string ApiKey { get { return _apiKey; } }
    }
}
