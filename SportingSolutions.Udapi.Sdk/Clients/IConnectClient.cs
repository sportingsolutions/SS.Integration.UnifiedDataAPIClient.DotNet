using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RestSharp;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public interface IConnectClient
    {
        IRestResponse<T> Login<T>(LoginRequiredDelegate<T> loginRequiredDelegate) where T : new();

        IRestResponse<T> Request<T>(Uri uri, Method method) where T : new();
        IRestResponse Request(Uri uri, Method method);
        IRestResponse<T> Request<T>(Uri uri, Method method, int timeout) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body, int timeout) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType) where T : new();
        IRestResponse<T> Request<T>(Uri uri, Method method, object body, string contentType, int timeout) where T : new();

        void RequestAsync<T>(Uri uri, Method method, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, Method method, int timeout, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, Method method, object body, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, Method method, object body, int timeout, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, Method method, object body, string contentType, Action<IRestResponse<T>> responseCallback) where T : new();
        void RequestAsync<T>(Uri uri, Method method, object body, string contentType, int timeout, Action<IRestResponse<T>> responseCallback) where T : new();
    }
}
