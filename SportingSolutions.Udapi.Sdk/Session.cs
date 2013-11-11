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
using System.Net;
using System.Text;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Exceptions;
using SportingSolutions.Udapi.Sdk.Extensions;
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

        internal Session(IConnectClient connectClient)
            : base(connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Session).ToString());
            GetRoot();
        }

        public IList<IService> GetServices()
        {
            Logger.Info("Get all available services..");
            if (_restItems == null)
            {
                GetRoot();
            }

            var result = _restItems.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient)).Cast<IService>().ToList();
            _restItems = null;
            return result;
        }

        public IService GetService(string name)
        {
            Logger.InfoFormat("Get Service {0}", name);
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

                var getRootResponse = ConnectClient.Login();
                messageStringBuilder.AppendFormat("GetRoot took {0}ms\r\n", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                if (getRootResponse.ErrorException != null || getRootResponse.Content == null)
                {
                    RestErrorHelper.LogRestError(Logger, getRootResponse, "GetRoot Http Error");
                    throw new Exception("There has been a problem calling GetRoot", getRootResponse.ErrorException);
                }

                if (getRootResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new NotAuthenticatedException("UserName or password are incorrect");
                }

                _restItems = getRootResponse.Content.FromJson<List<RestItem>>();
            }
            catch (NotAuthenticatedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error("Get Root Exception", ex);
                throw;
            }
            Logger.Debug(messageStringBuilder);
            stopwatch.Stop();
        }
    }
}
