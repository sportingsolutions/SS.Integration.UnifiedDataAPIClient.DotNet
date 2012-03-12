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
        protected NameValueCollection _headers;
        protected RestItem _state;
        protected IList<RestItem> _restItems;

        internal Endpoint()
        {
            
        }

        internal Endpoint(NameValueCollection headers, RestItem restItem)
        {
            _headers = headers;
            _state = restItem;
        }

        protected IEnumerable<RestItem> GetNext()
        {
            var result = new List<RestItem>();
            if(_state != null)
            {
                var theUrl = _state.Links[0].Href;
                result = RestHelper.GetResponse(new Uri(theUrl), null, "GET", "application/json", _headers).FromJson<List<RestItem>>();
            }
            return result;
        }

        protected IEnumerable<RestItem> GetRestItems(string name)
        {
            var result = new List<RestItem>();
            if (_restItems != null)
            {
                var theUrl = "";
                foreach (var restItem in _restItems.Where(restItem => restItem.Name == name))
                {
                    theUrl = restItem.Links[0].Href;
                    break;
                }
                result = RestHelper.GetResponse(new Uri(theUrl), null, "GET", "application/json", _headers).FromJson<List<RestItem>>();
            }
            return result;
        }
    }
}
