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
    public class Session : Endpoint, ISession
    {
        public Session(Uri serverUri, ICredentials credentials)
        {
            _headers = new NameValueCollection();
            GetRoot(serverUri, credentials);
        }

        public IList<IService> GetServices()
        {
            var serviceRestItems = GetRestItems("Services");
            return serviceRestItems.Select(serviceRestItem => new Service(_headers, serviceRestItem)).Cast<IService>().ToList();
        }
       
        public IService GetService(string name)
        {
            var serviceRestItems = GetRestItems("Services");
            return serviceRestItems.Select(serviceRestItem => new Service(_headers, serviceRestItem)).FirstOrDefault(service => service.Name == name);
        }

        private void GetRoot(Uri serverUri, ICredentials credentials)
        {
            HttpWebResponse response;
            try
            {
                response = RestHelper.GetResponseEx(serverUri, null, "GET", "application/json", _headers, 60000);
            }
            catch (WebException ex)
            {
                response = ex.Response as HttpWebResponse;
            }
            
            if(response != null && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var loginUrl = "";
                var items = RestHelper.GetResponse(response).FromJson<List<RestItem>>();
                foreach (var restItem in items.Where(restItem => restItem.Name == "Login"))
                {
                    loginUrl = restItem.Links[0].Href;
                    break;
                }
                _restItems = Login(new Uri(loginUrl), credentials);
            }
            else
            {
                _restItems = RestHelper.GetResponse(response).FromJson<List<RestItem>>();   
            }
        }

        private List<RestItem> Login(Uri serverUri, ICredentials credentials)
        {
            var headers = new NameValueCollection();
            headers.Add("X-Auth-User", credentials.UserName);
            headers.Add("X-Auth-Key", credentials.Password);

            var response = RestHelper.GetResponseEx(serverUri, null, "POST", "application/json", headers);
            _headers.Add("X-Auth-Token",response.Headers.Get("X-Auth-Token"));
            return RestHelper.GetResponse(response).FromJson<List<RestItem>>();
        }
    }
}
