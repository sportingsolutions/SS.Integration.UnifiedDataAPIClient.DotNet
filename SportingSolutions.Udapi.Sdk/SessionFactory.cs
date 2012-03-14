using System;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    public static class SessionFactory
    {
        public static ISession CreateSession(Uri serverUri, ICredentials credentials)
        {
            return new Session(serverUri, credentials);
        }
    }
}
