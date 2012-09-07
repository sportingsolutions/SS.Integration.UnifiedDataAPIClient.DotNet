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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk
{
    public abstract class Endpoint
    {
        protected NameValueCollection Headers;
        protected readonly RestItem State;

        internal Endpoint()
        {
            
        }

        internal Endpoint(NameValueCollection headers, RestItem restItem)
        {
            Headers = headers;
            State = restItem;
        }

        protected IEnumerable<RestItem> FindRelationAndFollow(string relation)
        {
            return FindRelationAndFollowAsString(relation).FromJson<List<RestItem>>();
        }

        protected string FindRelationAndFollowAsString(string relation)
        {
            var result = "";
            if (State != null)
            {
                var theLink = State.Links.First(restLink => restLink.Relation == relation);
                var theUrl = theLink.Href;
                result = RestHelper.GetResponse(new Uri(theUrl), null, "GET", "application/json", Headers, 5000);
            }
            return result;
        }
    }
}
