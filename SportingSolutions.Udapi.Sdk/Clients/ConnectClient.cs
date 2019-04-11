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
using SportingSolutions.Udapi.Sdk.Exceptions;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;
using Newtonsoft.Json;
using System.Net.Http;
using SportingSolutions.Udapi.Sdk.Extensions;

namespace SportingSolutions.Udapi.Sdk.Clients
{
   
    public class ConnectClient : IConnectClient
    {
        private const int DEFAULT_REQUEST_RETRY_ATTEMPTS = 3;
        private static readonly object sysLock = new object();

        private readonly Uri _baseUrl;
        private readonly ICredentials _credentials;

        private string _xAuthToken;

        public const string XAuthToken = "X-Auth-Token";
        public const string XAuthUser = "X-Auth-User";
        public const string XAuthKey = "X-Auth-Key";
        public const string JSON_CONTENT_TYPE = "application/json";

        private readonly ILog Logger;

        public ConnectClient(Uri baseUrl, ICredentials credentials)
        {
            if (baseUrl == null) throw new ArgumentNullException("baseUrl");
            if (credentials == null) throw new ArgumentNullException("credentials");

            _credentials = credentials;
            _baseUrl = baseUrl;

            Logger = LogManager.GetLogger(typeof(ConnectClient).ToString());
        }

        private HttpClient CreateClient()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;

            //var restClient = new RestClient();
            var handler = new TimeoutHandler { InnerHandler = new HttpClientHandler() };
            var httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromMilliseconds(UDAPI.Configuration.Timeout);
            
            //restClient.BaseUrl = _baseUrl;
            httpClient.BaseAddress = _baseUrl;

            //restClient.ClearHandlers();
            httpClient.DefaultRequestHeaders.Clear();
            
            //restClient.AddHandler("*", new ConnectConverter(UDAPI.Configuration.ContentType));
            //httpClient.DefaultRequestHeaders.Add("*", new ConnectConverter(UDAPI.Configuration.ContentType));

            if (!string.IsNullOrWhiteSpace(_xAuthToken))
            {
                //restClient.AddDefaultParameter(_xAuthTokenParameter);
                httpClient.DefaultRequestHeaders.Add(XAuthToken, _xAuthToken);
            }
            return httpClient;
        }

        private static HttpRequestMessage CreateRequest(Uri uri, HttpMethod method, object body, string contentType, int timeout)
        {
            //IRestRequest restRequest = new RestRequest(uri, method);
            var request = new HttpRequestMessage(method, uri);
            
            //restRequest.Timeout = timeout;
            request.SetTimeout(TimeSpan.FromMilliseconds(timeout));

            if (body != null)
            {
                //request.RequestFormat = DataFormat.Json;
                //request.JsonSerializer = new ConnectConverter(contentType);
                //request.AddBody(body);

                request.Content = new StringContent(body.ToString(), System.Text.Encoding.UTF8, JSON_CONTENT_TYPE);
                //?????????????? serializier ??????????
            }

            return request;
        }

        public HttpResponseMessage Login()
        {
            var client = CreateClient();
            var request = CreateRequest(_baseUrl, HttpMethod.Get, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);

            var response = client.Send(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var udapiItems = response.Content.Read<List<UdapiItem>>();
                var loginUri = FindLoginUri(udapiItems);
                if (loginUri != null)
                {
                    var loginRequest = CreateRequest(loginUri, HttpMethod.Post, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
                    loginRequest.Headers.Add(XAuthUser, _credentials.ApiUser);
                    loginRequest.Headers.Add(XAuthKey, _credentials.ApiKey);

                    response = client.Send(loginRequest);

                    if (response.StatusCode == HttpStatusCode.OK)
                        _xAuthToken = response.GetToken(XAuthToken);
                }
            }

            return response;
        }

        private static Uri FindLoginUri(IEnumerable<UdapiItem> udapiItems)
        {
            var restLink = udapiItems.SelectMany(restItem => restItem.Links)
                                           .FirstOrDefault(
                                               l => l.Relation == "http://api.sportingsolutions.com/rels/login");
            return new Uri(restLink.Href);
        }

        private bool Authenticate(HttpResponseMessage response)
        {
            var authenticated = false;
            var udapiItems = response.Content.ReadAsStringAsync().Result.FromJson<List<UdapiItem>>();

            var loginUri = FindLoginUri(udapiItems);

            var loginRequest = CreateRequest(loginUri, HttpMethod.Post, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);

            loginRequest.Headers.Add(XAuthUser, _credentials.ApiUser);
            loginRequest.Headers.Add(XAuthKey, _credentials.ApiKey);

            response = CreateClient().Send(loginRequest);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                _xAuthToken = response.GetToken(XAuthToken);
                authenticated = true;
            }
            return authenticated;
        }

        public HttpResponseMessage Request(Uri uri, HttpMethod method)
        {
            return Request(uri, method, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
        }

        public HttpResponseMessage Request(Uri uri, HttpMethod method, object body, string contentType, int timeout)
        {
            var connectionClosedRetryCounter = 0;
            HttpResponseMessage response = null;
            while (connectionClosedRetryCounter < DEFAULT_REQUEST_RETRY_ATTEMPTS)
            {
                var request = CreateRequest(uri, method, body, contentType, timeout);

                var client = CreateClient();
                var oldAuth = client.DefaultRequestHeaders.FirstOrDefault(x => x.Key == XAuthToken).Value.FirstOrDefault();

                try
                {
                    response = client.Send(request);
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        HttpErrorHelper.LogResponseWarn(Logger, response, string.Format("Unauthenticated when using authToken={0}", _xAuthToken != null ? _xAuthToken : String.Empty));

                        var authenticated = false;
                        lock (sysLock)
                        {

                            if (!string.IsNullOrWhiteSpace(_xAuthToken) || oldAuth == _xAuthToken)
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
                            try
                            {
                                var loginResponse = CreateClient().Send(request);
                                loginResponse.EnsureSuccessStatusCode();
                            }
                            catch(Exception ex)
                            {
                                connectionClosedRetryCounter++;
                                Logger.WarnFormat("Request failed due underlying connection closed URL={0}", uri);
                                continue;
                            }
                        }
                        else
                        {
                            response.ReasonPhrase = $"Not Authenticated";
                            throw new NotAuthenticatedException(response.ReasonPhrase);
                        }
                    }
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    connectionClosedRetryCounter++;
                    Logger.WarnFormat($"Request failed due underlying connection closed URL={uri}, reason={response.ReasonPhrase}");
                    continue;
                }

                connectionClosedRetryCounter = DEFAULT_REQUEST_RETRY_ATTEMPTS;
            }

            return response;
        }

        #region Sync Overloads

        public T Request<T>(Uri uri, HttpMethod method) where T : new()
        {
            return Request<T>(uri, method, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
        }

        public T Request<T>(Uri uri, HttpMethod method, int timeout) where T : new()
        {
            return Request<T>(uri, method, null, UDAPI.Configuration.ContentType, timeout);
        }

        public T Request<T>(Uri uri, HttpMethod method, object body) where T : new()
        {
            return Request<T>(uri, method, body, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout);
        }

        public T Request<T>(Uri uri, HttpMethod method, object body, int timeout) where T : new()
        {
            return Request<T>(uri, method, body, UDAPI.Configuration.ContentType, timeout);
        }

        public T Request<T>(Uri uri, HttpMethod method, object body, string contentType) where T : new()
        {
            return Request<T>(uri, method, body, contentType, UDAPI.Configuration.Timeout);
        }

        public T Request<T>(Uri uri, HttpMethod method, object body, string contentType, int timeout) where T : new()
        {
            return Request(uri, method, body, contentType, timeout).Content.Read<T>();
        }


        #endregion

        #region Async Overloads
        /*
        public void RequestAsync<T>(Uri uri, HttpMethod method, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, null, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, HttpMethod method, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, null, UDAPI.Configuration.ContentType, timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, HttpMethod method, object body, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, UDAPI.Configuration.ContentType, UDAPI.Configuration.Timeout, responseCallback);
        }

        public void RequestAsync<T>(Uri uri, HttpMethod method, object body, int timeout, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            RequestAsync(uri, method, body, UDAPI.Configuration.ContentType, timeout, responseCallback);
        }

        
        public void RequestAsync<T>(Uri uri, HttpMethod method, object body, string contentType, Action<IRestResponse<T>> responseCallback) where T : new()
        {
            
            RequestAsync(uri, method, body, contentType, UDAPI.Configuration.Timeout, responseCallback);
        }
        */
        #endregion

    }
}
