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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Exceptions;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;
using Newtonsoft.Json;

namespace SportingSolutions.Udapi.Sdk.Clients
{
   
    public class ConnectClient : IConnectClient
    {
        private const int DEFAULT_REQUEST_RETRY_ATTEMPTS = 3;
        private static readonly object sysLock = new object();

        private readonly Uri _baseUrl;
        private readonly ICredentials _credentials;

        private Parameter _xAuthTokenParameter;

        public const string XAuthToken = "X-Auth-Token";
        public const string XAuthUser = "X-Auth-User";
        public const string XAuthKey = "X-Auth-Key";

        private readonly ILog Logger;

        public ConnectClient(Uri baseUrl, ICredentials credentials)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException("baseUrl");
            _credentials = credentials ?? throw new ArgumentNullException("credentials");
            Logger = LogManager.GetLogger(typeof(ConnectClient).ToString());
        }

        private IRestClient CreateClient()
        {
            var restClient = new RestClient();
            restClient.BaseUrl = _baseUrl;
          
            restClient.ClearHandlers();
            restClient.AddHandler("*", new ConnectConverter(UDAPI.Configuration.ContentType));

            if (_xAuthTokenParameter != null)
            {
                restClient.AddDefaultParameter(_xAuthTokenParameter);
            }
            return restClient;
        }

        private static IRestRequest CreateRequest(Uri uri, Method method, object body, string contentType, int timeout)
        {
            IRestRequest request = new RestRequest(uri,method);
            
            request.Timeout = timeout;

            if (body != null)
            {
                request.RequestFormat = DataFormat.Json;
                request.JsonSerializer = new ConnectConverter(contentType);
                request.AddBody(body);
            }

            return request;
        }

        public IRestResponse Login()
        {
            var client = CreateClient();
            var request = CreateRequest(_baseUrl, Method.GET, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);

            var response = client.Execute(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var restItems = response.Content.FromJson<List<RestItem>>();
                var loginUri = FindLoginUri(restItems);
                if (loginUri != null)
                {
                    var loginRequest = CreateRequest(loginUri, Method.POST, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);

                    loginRequest.AddHeader(XAuthUser, _credentials.ApiUser);
                    loginRequest.AddHeader(XAuthKey, _credentials.ApiKey);

                    response = client.Execute(loginRequest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        _xAuthTokenParameter = response.Headers.FirstOrDefault(h => h.Name == XAuthToken);
                    }
                }
            }

            return response;
        }

        private static Uri FindLoginUri(IEnumerable<RestItem> restItems)
        {
            var restLink = restItems.SelectMany(restItem => restItem.Links)
                                           .FirstOrDefault(
                                               l => l.Relation == "http://api.sportingsolutions.com/rels/login");
            return new Uri(restLink.Href);
        }

        private bool Authenticate(IRestResponse response)
        {
            var authenticated = false;
            var restItems = response.Content.FromJson<List<RestItem>>();

            var loginUri = FindLoginUri(restItems);

            var loginRequest = CreateRequest(loginUri, Method.POST, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);

            loginRequest.AddHeader(XAuthUser, _credentials.ApiUser);
            loginRequest.AddHeader(XAuthKey, _credentials.ApiKey);

            response = CreateClient().Execute<List<RestItem>>(loginRequest);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _xAuthTokenParameter = response.Headers.FirstOrDefault(h => h.Name == XAuthToken);
                authenticated = true;
            }
            return authenticated;
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType, int timeout) where T : new()
        {
            var restResponse = Request(uri, method, body, contentType, timeout);
            var response = new RestResponse<T>
            {
                Request = restResponse.Request,
                StatusCode = restResponse.StatusCode,
                Content = restResponse.Content
            };

            if (restResponse.ErrorException != null)
            {
                response.ErrorException = restResponse.ErrorException;
            }
            else
            {
                try
                {
                    response.Data = restResponse.Content.FromJson<T>();
                }
                catch (JsonSerializationException ex)
                {
                    throw new JsonSerializationException($"Serialization exception from JSON={restResponse.Content}",
                        ex);
                }
                catch (Exception ex)
                {
                    response.ErrorException = ex;
                }
            }

            return response;
        }

        public IRestResponse Request(Uri uri, Method method, object body, string contentType, int timeout)
        {
            var connectionClosedRetryCounter = 0;
            IRestResponse response = null;
            while (connectionClosedRetryCounter < DEFAULT_REQUEST_RETRY_ATTEMPTS)
            {
                var request = CreateRequest(uri, method, body, contentType, timeout);

                var client = CreateClient();
                var oldAuth = client.DefaultParameters.FirstOrDefault(x => x.Name == XAuthToken);

                response = client.Execute(request);

                if (response.ResponseStatus == ResponseStatus.Error &&
                    response.ErrorException is WebException &&
                    ((WebException)response.ErrorException).Status == WebExceptionStatus.KeepAliveFailure)
                {
                    //retry
                    connectionClosedRetryCounter++;
                    Logger.WarnFormat("Request failed due underlying connection closed URL={0}", uri);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    RestErrorHelper.LogRestWarn(Logger, response, string.Format("Unauthenticated when using authToken={0}", _xAuthTokenParameter != null ? _xAuthTokenParameter.Value : String.Empty));

                    var authenticated = false;
                    lock (sysLock)
                    {

                        if (_xAuthTokenParameter == null || oldAuth == _xAuthTokenParameter)
                        {
                            authenticated = Authenticate(response);
                        }
                        else
                        {
                            authenticated = true;
                        }
                    }

                    if (authenticated)
                    {
                        request = CreateRequest(uri, method, body, contentType, timeout);
                        response = CreateClient().Execute(request);

                        if (response.ResponseStatus == ResponseStatus.Error &&
                            response.ErrorException is WebException &&
                            ((WebException)response.ErrorException).Status == WebExceptionStatus.KeepAliveFailure)
                        {
                            //retry
                            connectionClosedRetryCounter++;
                            Logger.WarnFormat("Request failed due underlying connection closed URL={0}", uri);
                            continue;
                        }
                    }
                    else
                    {
                        throw new NotAuthenticatedException(string.Format("Not Authenticated for url={0}", uri));
                    }
                }

                connectionClosedRetryCounter = DEFAULT_REQUEST_RETRY_ATTEMPTS;
            }

            return response;
        }

        //Not used
        public void RequestAsync<T>(Uri uri, Method method, object body, string contentType, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            var request = CreateRequest(uri, method, body, contentType, timeout);

            CreateClient().ExecuteAsync(request, responseCallback);
        }

        #region Sync Overloads

        public IRestResponse<T> Request<T>(Uri uri, Method method) where T : new()
        {
            return Request<T>(uri, method, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
        }

        public IRestResponse Request(Uri uri, Method method)
        {
            return Request(uri, method, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, int timeout) where T : new()
        {
            return Request<T>(uri, method, null, UDAPI.Configuration.ContentType, timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body) where T : new()
        {
            return Request<T>(uri, method, body, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body, int timeout) where T : new()
        {
            return Request<T>(uri, method, body, UDAPI.Configuration.ContentType, timeout);
        }

        public IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType) where T : new()
        {
            return Request<T>(uri, method, body, contentType, UDAPI.Configuration.Timeout);
        }

        #endregion

        #region Async Overloads

        public void RequestAsync<T>(Uri uri, Method method, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, null, UDAPI.Configuration.ContentType, timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, UDAPI.Configuration.ContentType, timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, Method method, object body, string contentType, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, contentType, UDAPI.Configuration.Timeout, responseCallback);
        }

        #endregion
    }
}
