using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Spin.TradingServices.Udapi.Sdk.Extensions
{
    public static class JsonHelper
    {
        public static T FromJson<T>(this string json, bool expectIsoDate = true)
        {
            return (T)JsonConvert.DeserializeObject(json, typeof(T), new JsonSerializerSettings { Converters = expectIsoDate ? new List<JsonConverter> { new IsoDateTimeConverter() } : null, NullValueHandling = NullValueHandling.Ignore });
        }
    }
}
