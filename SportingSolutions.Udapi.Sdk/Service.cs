using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk
{
    public class Service : Endpoint, IService
    {
        internal Service(NameValueCollection headers, RestItem restItem):base(headers, restItem)
        {
            
        }

        public string Name
        {
            get { return _state.Name; }
        }

        public List<IFeature> GetFeatures()
        {
            var restItems = GetNext();
            return restItems.Select(restItem => new Feature(_headers, restItem)).Cast<IFeature>().ToList();
        }

        public IFeature GetFeature(string name)
        {
            var restItems = GetNext();
            return (from restItem in restItems where restItem.Name == name select new Feature(_headers, restItem)).FirstOrDefault();
        }
    }
}
