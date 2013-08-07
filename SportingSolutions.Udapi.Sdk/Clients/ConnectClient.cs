using System;
using System.Linq;
using System.Net;
using RestSharp;
using RestSharp.Deserializers;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public delegate Uri LoginRequiredDelegate<T>(IRestResponse<T> restResponse);

    public class ConnectClient : IConnectClient
    {
        private readonly IConfiguration _configuration;
        private readonly ICredentials _credentials;

        private Parameter _xAuthTokenParameter;

        public const string XAuthToken = "X-Auth-Token";
        public const string XAuthUser = "X-Auth-User";
        public const string XAuthKey = "X-Auth-Key";

        public ConnectClient(IConfiguration configuration, ICredentials credentials)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            if (credentials == null) throw new ArgumentNullException("credentials");

            _configuration = configuration;
            _credentials = credentials;
        }

        private IRestClient CreateClient()
        {
            var restClient = new RestClient();

            restClient.ClearHandlers();
            restClient.AddHandler("application/xml", new XmlDeserializer());
            restClient.AddHandler("*", new ConnectConverter(_configuration.ContentType));

            if (_xAuthTokenParameter != null)
            {
                restClient.AddDefaultParameter(_xAuthTokenParameter);
            }

            return restClient;
        }

        private static IRestRequest CreateRequest(Uri uri, Method method, object body, string contentType, int timeout)
        {
            IRestRequest request = new RestRequest(uri, method);
            request.Resource = uri.ToString();
            request.Timeout = timeout;

            if (body != null)
            {
                request.RequestFormat = DataFormat.Json;
                request.JsonSerializer = new ConnectConverter(contentType);
                request.AddBody(body);
            }

            return request;
        }

        public IRestResponse<T> Login<T>(LoginRequiredDelegate<T> loginRequiredDelegate) where T : new()
        {
            var client = CreateClient();
            var request = CreateRequest(_configuration.BaseUrl, Method.GET, null, _configuration.ContentType, _configuration.Timeout);

            var response = client.Execute<T>(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var loginUri = loginRequiredDelegate(response);
                if (loginUri != null)
                {
                    var loginRequest = CreateRequest(loginUri, Method.POST, null, _configuration.ContentType, _configuration.Timeout);

                    loginRequest.AddHeader(XAuthUser, _credentials.ApiUser);
                    loginRequest.AddHeader(XAuthKey, _credentials.ApiKey);

                    response = client.Execute<T>(loginRequest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        _xAuthTokenParameter = response.Headers.FirstOrDefault(h => h.Name == XAuthToken);
                        client = CreateClient();

                        response = client.Execute<T>(request);
                    }
                }
            }

            return response;
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType, int timeout) where T : new()
        {
            var request = CreateRequest(uri, method, body, contentType, timeout);

            var response = CreateClient().Execute<T>(request);

            return response;
        }

        public IRestResponse Request(Uri uri, Method method, object body, string contentType, int timeout)
        {
            var request = CreateRequest(uri, method, body, contentType, timeout);
            var response = CreateClient().Execute(request);
            return response;
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, string contentType, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            var request = CreateRequest(uri, method, body, contentType, timeout);

            CreateClient().ExecuteAsync(request, responseCallback);
        }

        #region Sync Overloads

        public IRestResponse<T> Request<T>(Uri uri, Method method) where T : new()
        {
            return Request<T>(uri, method, null, _configuration.ContentType, _configuration.Timeout);
        }

        public IRestResponse Request(Uri uri, Method method)
        {
            return Request(uri, method, null, _configuration.ContentType, _configuration.Timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, int timeout) where T : new()
        {
            return Request<T>(uri, method, null, _configuration.ContentType, timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body) where T : new()
        {
            return Request<T>(uri, method, body, _configuration.ContentType, _configuration.Timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body, int timeout) where T : new()
        {
            return Request<T>(uri, method, body, _configuration.ContentType, timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType) where T : new()
        {
            return Request<T>(uri, method, body, contentType, _configuration.Timeout);
        }

        #endregion

        #region Async Overloads

        public void RequestAsync<T>(Uri uri, Method method, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, null, _configuration.ContentType, _configuration.Timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, null, _configuration.ContentType, timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, _configuration.ContentType, _configuration.Timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, _configuration.ContentType, timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, string contentType, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, contentType, _configuration.Timeout, responseCallback);
        }

        #endregion
    }
}
