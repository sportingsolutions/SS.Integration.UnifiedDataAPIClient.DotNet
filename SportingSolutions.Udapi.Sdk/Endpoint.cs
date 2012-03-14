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
            var result = new List<RestItem>();
            if(State != null)
            {
                var theLink = State.Links.First(restLink => restLink.Relation == relation);
                var theUrl = theLink.Href;
                result = RestHelper.GetResponse(new Uri(theUrl), null, "GET", "application/json", Headers).FromJson<List<RestItem>>();
            }
            return result;
        }
    }
}
