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

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SportingSolutions.Udapi.Sdk.Extensions
{
    public static class JsonHelper
    {
        public static T FromJson<T>(this string json, bool expectIsoDate = true)
        {
            return (T)JsonConvert.DeserializeObject(json, typeof(T), new JsonSerializerSettings { Converters = expectIsoDate ? new List<JsonConverter> { new IsoDateTimeConverter() } : null, NullValueHandling = NullValueHandling.Ignore });
        }

        public static string ToJson(this object deserializedObject, bool expectIsoDate = true)
        {
            string serializedObject = null;
            if(deserializedObject != null)
            {
                serializedObject = JsonConvert.SerializeObject(deserializedObject, Formatting.None,
                                            new JsonSerializerSettings
                                                {
                                                    Converters =
                                                        expectIsoDate
                                                            ? new List<JsonConverter> {new IsoDateTimeConverter()}
                                                            : null,
                                                    NullValueHandling = NullValueHandling.Ignore
                                                });
            }
            return serializedObject;
        }
    }
}
