using System;
using Spin.TradingServices.Udapi.Sdk.Interfaces;

namespace Spin.TradingServices.Udapi.Sdk
{
    public class SessionFactory
    {
        public static ISession CreateSession(Uri serverUri, ICredentials credentials)
        {
            return new Session(serverUri, credentials);
        }
    }
}
