using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using SportingSolutions.Udapi.Sdk.Clients;

namespace SportingSolutions.Udapi.Sdk.Extensions
{
    public static class HttpResponseExtension
    {
        public static string Read(this HttpContent content)
        {
            return content?.ReadAsStringAsync().Result;
        }

        public static T Read<T>(this HttpContent content)
        {
            string contentString = string.Empty;
            T result = default(T);
            try
            {
                contentString = content.Read();
                if (contentString != null)
                    result = new ConnectConverter(UDAPI.Configuration.ContentType).Deserialize<T>(contentString);
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
