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
