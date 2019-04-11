using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace SportingSolutions.Udapi.Sdk.Extensions
{
    public static class HttpClientExtension
    {
        public static HttpResponseMessage Send(this HttpClient client, HttpRequestMessage request)
        {
            return client.SendAsync(request).Result;
        }

        public static HttpResponseMessage Send(this HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return client.SendAsync(request, cancellationToken).Result;
        }

        /// <summary>
        /// timeout in ms
        /// </summary>
        public static HttpResponseMessage Send(this HttpClient client, HttpRequestMessage request, int timeout)
        {
            var cts = new CancellationTokenSource(timeout); 
            return client.SendAsync(request, cts.Token).Result;
        }

        public static T Send<T>(this HttpClient httpClient, HttpRequestMessage request)
        {
            var response = httpClient.Send(request);
            return response.Content.Read<T>();
        }

        public static T Send<T>(this HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = httpClient.Send(request, cancellationToken);
            return response.Content.Read<T>();
        }

        public static async Task<T> SendAsync<T>(this HttpClient httpClient, HttpRequestMessage request)
        {
            var response = await httpClient.SendAsync(request);
            return response.Content.Read<T>();
        }

        public static async Task<T> SendAsync<T>(this HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            return response.Content.Read<T>();
        }
    }
}
