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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;
using Newtonsoft.Json;
using System.Net.Http;
using SportingSolutions.Udapi.Sdk.Extensions;

namespace SportingSolutions.Udapi.Sdk
{
    public abstract class Endpoint
    {
        protected readonly UdapiItem State;

        protected readonly IConnectClient ConnectClient;

        protected ILog Logger;

        internal Endpoint(IConnectClient connectClient)
        {
            ConnectClient = connectClient;
        }

        internal Endpoint(UdapiItem udapiItem, IConnectClient connectClient)
        {
            State = udapiItem;
            ConnectClient = connectClient;
        }

        protected Uri FindRelationUri(string relation)
        {
            var theLink = State.Links.First(udapiLink => udapiLink.Relation == relation);
            var theUrl = theLink.Href;
            return new Uri(theUrl);
        }

        protected IEnumerable<UdapiItem> FindRelationAndFollow(string relation, string errorHeading, StringBuilder loggingStringBuilder)
        {
            var stopwatch = new Stopwatch();

            IEnumerable<UdapiItem> result = new List<UdapiItem>();
            if (State != null)
            {
                var theUri = FindRelationUri(relation);

                loggingStringBuilder.AppendFormat("Call to url={0} ", theUri);
                HttpResponseMessage response = null;
                stopwatch.Start();
                int tryIterationCounter = 1;
                while (tryIterationCounter <= 3)
                {
                    try
                    {
                        //response = ConnectClient.Request<List<UdapiItem>>(theUri, HttpMethod.Get);
                        response = ConnectClient.Request(theUri, HttpMethod.Get);
                        result = response.Content.Read<List<UdapiItem>>();
                        break;
                    }
                    catch (JsonSerializationException ex)
                    {
                        if (tryIterationCounter == 3)
                        {
                            Logger.Warn($"JsonSerializationException Method=FindRelationAndFollow {ex}");
                            return null;
                        }
                        Thread.Sleep(1000);
                    }
                    tryIterationCounter++;
                }

                stopwatch.Stop();
                loggingStringBuilder.AppendFormat("took duration={0}ms - ", stopwatch.ElapsedMilliseconds);
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    HttpErrorHelper.LogResponseError(Logger, response, errorHeading);
                    throw new Exception($"Error calling {theUri} - ", ex);
                }
            }

            return result;
        }
        

        protected string FindRelationAndFollowAsString(string relation, string errorHeading, StringBuilder loggingStringBuilder)
        {
            var stopwatch = new Stopwatch();
            var result = string.Empty;
            if (State != null)
            {
                var theUri = FindRelationUri(relation);

                loggingStringBuilder.AppendFormat($"Beginning call to url={theUri}");
                stopwatch.Start();
                var response = ConnectClient.Request(theUri, HttpMethod.Get);
                stopwatch.Stop();
                loggingStringBuilder.AppendFormat($"took duration={stopwatch.ElapsedMilliseconds}ms");
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    if (response.Content == null)
                    {
                        HttpErrorHelper.LogResponseError(Logger, response, errorHeading);
                        throw new Exception(string.Format($"Error calling {theUri}"), ex);
                    }
                }
                result = response.Content.ReadAsStringAsync().Result;

            }
            return result;
        }
    }
}
