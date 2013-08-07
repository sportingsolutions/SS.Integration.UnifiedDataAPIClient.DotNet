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
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using ICredentials = SportingSolutions.Udapi.Sdk.Interfaces.ICredentials;

namespace SportingSolutions.Udapi.Sdk
{
    public static class SessionFactory
    {
        public static ISession CreateSession(Uri serverUri, ICredentials credentials)
        {
            var connectClient = new ConnectClient(new Configuration(serverUri), new Clients.Credentials(credentials.UserName, credentials.Password));
            return new Session(connectClient);
        }
    }
}
