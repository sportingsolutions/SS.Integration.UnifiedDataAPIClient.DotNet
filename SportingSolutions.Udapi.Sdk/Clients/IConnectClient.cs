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
using RestSharp;
using System.Net.Http;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public interface IConnectClient
    {
        IRestResponse Login();

        IRestResponse<T> Request<T>(Uri uri, HttpMethod method) where T : new();
        IRestResponse Request(Uri uri, HttpMethod method, object body, string contentType, int timeout);
        IRestResponse Request(Uri uri, HttpMethod method);
        IRestResponse<T> Request<T>(Uri uri, HttpMethod method, int timeout) where T : new();
        IRestResponse<T> Request<T>(Uri uri, HttpMethod method, object body) where T : new();
        IRestResponse<T> Request<T>(Uri uri, HttpMethod method, object body, int timeout) where T : new();
        IRestResponse<T> Request<T>(Uri uri, HttpMethod method, object body, string contentType) where T : new();
        IRestResponse<T> Request<T>(Uri uri, HttpMethod method, object body, string contentType, int timeout) where T : new();

        void RequestAsync<T>(Uri uri, HttpMethod method, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, HttpMethod method, int timeout, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, HttpMethod method, object body, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, HttpMethod method, object body, int timeout, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, HttpMethod method, object body, string contentType, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, HttpMethod method, object body, string contentType, int timeout, Action<IRestResponse<T>> responseCallback) where T : new();
    }
}
