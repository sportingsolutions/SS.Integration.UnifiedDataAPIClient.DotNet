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
using System.Diagnostics;
using System.Linq;
using System.Text;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public abstract class Endpoint
    {
        protected NameValueCollection Headers;
        protected readonly RestItem State;

        protected readonly IConnectClient _connectClient;

        protected ILog _logger;

        internal Endpoint(IConnectClient connectClient)
        {
            _connectClient = connectClient;
        }

        internal Endpoint(NameValueCollection headers, RestItem restItem, IConnectClient connectClient)
        {
            Headers = headers;
            State = restItem;
            _connectClient = connectClient;
        }

        protected IEnumerable<RestItem> FindRelationAndFollow(string relation, string errorHeading, StringBuilder loggingStringBuilder)
        {
            var stopwatch = new Stopwatch();
            
            IEnumerable<RestItem> result = new List<RestItem>();
            if (State != null)
            {
                var theLink = State.Links.First(restLink => restLink.Relation == relation);
                var theUrl = theLink.Href;

                loggingStringBuilder.AppendFormat("Beginning call to url={0} \r\n",theUrl);
                stopwatch.Start();
                var response = _connectClient.Request<List<RestItem>>(new Uri(theUrl), Method.GET);

                loggingStringBuilder.AppendFormat("took duration={0}ms\r\n", stopwatch.ElapsedMilliseconds);
                if (response.ErrorException != null)
                {
                    RestErrorHelper.LogRestError(_logger, response, errorHeading);
                    return result;
                }

                result = response.Data;
            }

            stopwatch.Stop();
            return result;
        }

        protected string FindRelationAndFollowAsString(string relation, string errorHeading, StringBuilder loggingStringBuilder)
        {
            var stopwatch = new Stopwatch();
            var result = string.Empty;
            if (State != null)
            {
                var theLink = State.Links.First(restLink => restLink.Relation == relation);
                var theUrl = theLink.Href;

                loggingStringBuilder.AppendFormat("Beginning call to url={0} \r\n", theUrl);
                stopwatch.Start();
                var response = _connectClient.Request(new Uri(theUrl), Method.GET);
                loggingStringBuilder.AppendFormat("took duration={0}ms\r\n", stopwatch.ElapsedMilliseconds);
                if (response.ErrorException != null || response.Content == null)
                {
                    RestErrorHelper.LogRestError(_logger, response, errorHeading);
                    return result;
                }
                result = response.Content;
            }
            stopwatch.Stop();
            return result;
        }
    }
}
