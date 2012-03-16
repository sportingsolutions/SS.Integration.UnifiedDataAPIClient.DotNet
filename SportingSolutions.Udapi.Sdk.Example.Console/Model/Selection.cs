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

namespace SportingSolutions.Udapi.Sdk.Example.Console.Model
{
    public class Selection
    {
        public Selection()
        {
            Tags = new Dictionary<string, object>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public Dictionary<string, object> Tags { get; set; }

        public string DisplayPrice { get; set; }

        public double? Price { get; set; }

        public string Status { get; set; }

        public bool? Tradable { get; set; }
    }
}
