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
    public class Session : Endpoint, ISession
    {
        internal Session(IConnectClient connectClient)
            : base(connectClient)
        {
            Logger = LogManager.GetLogger(typeof(Session).ToString());
        }

        public IList<IService> GetServices()
        {
            Logger.Debug("Get all available services...");
            var links = GetRoot();
            if (links == null)
                return new List<IService>();
            
            return links.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient)).Cast<IService>().ToList();
        }

        public IService GetService(string name)
        {
            Logger.DebugFormat("Get service={0}", name);
            var links = GetRoot();

            if (links == null)
                return null;

            return links.Select(serviceRestItem => new Service(serviceRestItem, ConnectClient)).FirstOrDefault(service => service.Name == name);
        }

        private IEnumerable<UdapiItem> GetRoot()
        {
            var stopwatch = new Stopwatch();
            var messageStringBuilder = new StringBuilder("GetRoot request...");
            try
            {
                stopwatch.Start();

                var getRootResponse = ConnectClient.Login();
                messageStringBuilder.AppendFormat("took {0}ms", stopwatch.ElapsedMilliseconds);
                stopwatch.Restart();

                if (getRootResponse.ErrorException != null || getRootResponse.Content == null)
                {
                    RestErrorHelper.LogResponseError(Logger, getRootResponse, "GetRoot HTTP error");
                    throw new Exception("Error calling GetRoot", getRootResponse.ErrorException);
                }

                if (getRootResponse.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new NotAuthenticatedException("Username or password are incorrect");
                }

                if (getRootResponse.Content != null)
                    return getRootResponse.Content.FromJson<List<UdapiItem>>();
            }
            catch (NotAuthenticatedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error("GetRoot exception", ex);
                throw;
            }
            finally
            {
                Logger.Debug(messageStringBuilder);
                stopwatch.Stop();
            }

            return null;
        }
    }
}
