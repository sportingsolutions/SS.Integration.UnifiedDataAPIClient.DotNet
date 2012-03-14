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
            get { return State.Name; }
        }

        public List<IFeature> GetFeatures()
        {
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/features/list");
            return restItems.Select(restItem => new Feature(Headers, restItem)).Cast<IFeature>().ToList();
        }

        public IFeature GetFeature(string name)
        {
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/features/list");
            return (from restItem in restItems where restItem.Name == name select new Feature(Headers, restItem)).FirstOrDefault();
        }
    }
}
