using System;
using System.Collections.Generic;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Interfaces;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Udapi
{
    internal class Udapi : BaseSS<IService>, IService
    {
        private readonly ILog _simpleLogger;

        internal Udapi()
        {
            _logger = LogManager.GetLogger(typeof(Udapi).ToString());
            _simpleLogger = LogManager.GetLogger("SimpleUDAPILogger");
            try
            {
                //Assign the method that is needed to get a fresh instance of the real service
                TheReconnectMethod = InitUdapiService;
                Init(true);
            }
            catch (Exception)
            {
                _simpleLogger.Error("Unable to connect to the GTP-UDAPI. Check the Evenue adapter is running ok.");
                throw;
            }
        }

        private void InitUdapiService()
        {
            _logger.Debug("UDAPI, Getting Service");
            _theRealObject = Session.GetService("UnifiedDataAPI");
            _logger.Debug("UDAPI, Retrieved Service");
        }

        public List<IFeature> GetFeatures()
        {
            try
            {
                var realFeatures = ReconnectOnException(x => x.GetFeatures(), _theRealObject);
                return realFeatures != null ? realFeatures.Select(feature => new UdapiFeature(feature.Name, feature)).Cast<IFeature>().ToList() : new List<IFeature>();
            }
            catch (Exception)
            {
                _simpleLogger.Error("Unable to retrieve sports from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.");
                throw;
            }
        }

        public IFeature GetFeature(string name)
        {
            try
            {
                var realFeature = ReconnectOnException(x => x.GetFeature(name), _theRealObject);
                return realFeature != null ? new UdapiFeature(name, realFeature) : null;
            }
            catch (Exception)
            {
                _simpleLogger.ErrorFormat("{0} - Unable to retrieve sport from GTP-UDAPI after multiple attempts. Check the Evenue adapter is running ok.",name);
                throw;
            }
        }

        public string Name
        {
            get { return ReconnectOnException(x => x.Name, _theRealObject); }
        }
    }
}
