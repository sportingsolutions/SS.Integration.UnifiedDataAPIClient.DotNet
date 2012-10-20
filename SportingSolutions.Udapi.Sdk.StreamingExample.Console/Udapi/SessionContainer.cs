using System;
using SportingSolutions.Udapi.Sdk.Interfaces;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Udapi
{
    public class SessionContainer 
    {
        private static volatile ISession _theSession;
        private static readonly object SyncRoot = new Object();

        private readonly ILog _logger = LogManager.GetLogger(typeof(SessionContainer).ToString());
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
                    lock (SyncRoot)
                    {
                        if (_theSession == null)
                        {
                            _logger.Info("Connecting to UDAPI....");
                            _theSession = SessionFactory.CreateSession(_url, _credentials);
                            _logger.Info("Successfully connected to UDAPI.");
                        }
                    }
                }

                return _theSession;
            }
        }

        public void ReleaseSession()
        {
            lock (SyncRoot)
            {
                _theSession = null;
            }
        }
    }
}
