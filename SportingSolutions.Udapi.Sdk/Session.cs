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
using System.Net;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using ICredentials = SportingSolutions.Udapi.Sdk.Interfaces.ICredentials;

namespace SportingSolutions.Udapi.Sdk
{
    /// <summary>
    /// 
    /// </summary>
    public class Session : Endpoint, ISession
    {
        private IList<RestItem> _restItems;

        public Session(Uri serverUri, ICredentials credentials)
        {
            Headers = new NameValueCollection();
            GetRoot(serverUri, credentials);
        }

        public IList<IService> GetServices()
        {
            return _restItems.Select(serviceRestItem => new Service(Headers, serviceRestItem)).Cast<IService>().ToList();
        }
       
        public IService GetService(string name)
        {
            return _restItems.Select(serviceRestItem => new Service(Headers, serviceRestItem)).FirstOrDefault(service => service.Name == name);
        }

        private void GetRoot(Uri serverUri, ICredentials credentials)
        {
            HttpWebResponse response;
            try
            {
                response = RestHelper.GetResponseEx(serverUri, null, "GET", "application/json", Headers, 60000);
            }
            catch (WebException ex)
            {
                response = ex.Response as HttpWebResponse;
            }
            
            if(response != null && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var items = RestHelper.GetResponse(response).FromJson<List<RestItem>>();

                var loginLink = items.SelectMany(restItem => restItem.Links).First(
                    restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/v001/login");
                var loginUrl = loginLink.Href;
                
                _restItems = Login(new Uri(loginUrl), credentials);
            }
            else
            {
                _restItems = RestHelper.GetResponse(response).FromJson<List<RestItem>>();   
            }
        }

        private List<RestItem> Login(Uri serverUri, ICredentials credentials)
        {
            var headers = new NameValueCollection
                              {{"X-Auth-User", credentials.UserName}, {"X-Auth-Key", credentials.Password}};

            var response = RestHelper.GetResponseEx(serverUri, null, "POST", "application/json", headers);
            Headers.Add("X-Auth-Token",response.Headers.Get("X-Auth-Token"));
            return RestHelper.GetResponse(response).FromJson<List<RestItem>>();
        }
    }
}
