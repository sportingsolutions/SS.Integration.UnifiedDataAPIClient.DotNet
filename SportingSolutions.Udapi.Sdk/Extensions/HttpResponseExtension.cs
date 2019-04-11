using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;

namespace SportingSolutions.Udapi.Sdk.Extensions
{
    public static class HttpResponseExtension
    {
        public static T Read<T>(this HttpContent content)
        {
            string contentString = string.Empty;
            T result = default(T);
            try
            {
                contentString = content.ReadAsStringAsync().Result;
                result = contentString.FromJson<T>();
            }
            catch(JsonSerializationException ex)
            {
                throw new JsonSerializationException($"Serialization exception from JSON={contentString}",  ex);
            }
            return result;
        }

        public static string GetToken(this HttpResponseMessage response, string tokenName)
        {
            var header = response.Headers.FirstOrDefault(h => h.Key == tokenName);
            return header.Value.FirstOrDefault();
        }
    }
}
