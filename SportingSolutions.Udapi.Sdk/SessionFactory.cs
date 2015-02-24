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
using System.Collections.Concurrent;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using ICredentials = SportingSolutions.Udapi.Sdk.Interfaces.ICredentials;

namespace SportingSolutions.Udapi.Sdk
{
    public class SessionFactory
    {
        private static readonly SessionFactory _sessionFactory = new SessionFactory();
        private readonly ConcurrentDictionary<string, ISession> _sessions;

        private SessionFactory()
        {
            _sessions = new ConcurrentDictionary<string, ISession>();
        }

        public static ISession CreateSession(Uri serverUri, ICredentials credentials)
        {
            return _sessionFactory.GetSession(serverUri, credentials);
        }

        private ISession GetSession(Uri serverUri, ICredentials credentials)
        {
            ISession session = null;
            _sessions.TryGetValue(serverUri + credentials.UserName, out session);
            if (session == null)
            {
                var connectClient = new ConnectClient(new Configuration(serverUri), new Clients.Credentials(credentials.UserName, credentials.Password));
                session = new Session(connectClient);
                _sessions.TryAdd(serverUri + credentials.UserName, session);
            }
            return session;
        }
    }
}
