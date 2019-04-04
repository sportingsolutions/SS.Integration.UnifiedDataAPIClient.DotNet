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

        public static T Send<T>(this HttpClient httpClient, HttpRequestMessage request)
        {
            var response = httpClient.Send(request);
            return response.Content.ReadAsStringAsync().Result.FromJson<T>();
        }

        public static T Send<T>(this HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = httpClient.Send(request, cancellationToken);
            return response.Content.ReadAsStringAsync().Result.FromJson<T>();
        }
    }
}
