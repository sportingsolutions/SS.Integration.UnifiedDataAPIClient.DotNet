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
