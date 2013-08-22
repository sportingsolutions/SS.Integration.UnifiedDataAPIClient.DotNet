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
using System.Net;
using System.Text;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    /// <summary>
    /// 
    /// </summary>
    public class Session : Endpoint, ISession
    {
        private IList<RestItem> _restItems;

        internal Session(IConnectClient connectClient) : base(connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Session).ToString());
            GetRoot();
        }

        public IList<IService> GetServices()
        {
            Logger.Info("Get all available services..");
            if(_restItems == null)
            {
                GetRoot();
            }

            var result = _restItems.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient)).Cast<IService>().ToList();
            _restItems = null;
            return result;
        }
       
        public IService GetService(string name)
        {
            Logger.InfoFormat("Get Service {0}",name);
            if (_restItems == null)
            {
                GetRoot();
            }

            var result = _restItems.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient)).FirstOrDefault(service => service.Name == name);
            _restItems = null;
            return result;
        }

        private void GetRoot()
        {
            var stopwatch = new Stopwatch();
            var messageStringBuilder = new StringBuilder("Beginning Get Root Request");
            try
            {
                stopwatch.Start();

                LoginRequiredDelegate<List<RestItem>> loginRequired = delegate(IRestResponse<List<RestItem>> response)
                    {
                        messageStringBuilder.Append("Login Required\r\n");

                        var restLink = response.Data.SelectMany(restItem => restItem.Links)
                                               .FirstOrDefault(
                                                   l => l.Relation == "http://api.sportingsolutions.com/rels/login");
                        return new Uri(restLink.Href);
                    };

                var getRootResponse = ConnectClient.Login(loginRequired);
                messageStringBuilder.AppendFormat("GetRoot took {0}ms\r\n", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                if (getRootResponse.ErrorException != null || getRootResponse.Data == null || !getRootResponse.Data.Any())
                {
                    RestErrorHelper.LogRestError(Logger, getRootResponse, "GetRoot Http Error");
                    return;
                }

                _restItems = getRootResponse.Data;
                
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse) ex.Response;
                Logger.Error("Get Root Web Exception",ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Get Root Exception", ex);
            }
            Logger.Debug(messageStringBuilder);
            stopwatch.Stop();
        }
    }
}
