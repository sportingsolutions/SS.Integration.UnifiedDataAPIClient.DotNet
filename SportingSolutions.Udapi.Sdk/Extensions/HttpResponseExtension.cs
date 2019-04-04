using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace SportingSolutions.Udapi.Sdk.Extensions
{
    public static class HttpResponseExtension
    {
        public static T Read<T>(this HttpContent content)
        {
            return content.ReadAsStringAsync().Result.FromJson<T>();
        }

        public static string GetToken(this HttpResponseMessage response, string tokenName)
        {
            var header = response.Headers.FirstOrDefault(h => h.Key == tokenName);
            return header.Value.FirstOrDefault();
        }
    }
}
