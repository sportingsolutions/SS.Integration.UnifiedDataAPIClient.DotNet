using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk
{
    public class Feature : Endpoint, IFeature
    {
        internal Feature(NameValueCollection headers, RestItem restItem):base(headers, restItem)
        {
            
        }

        public string Name
        {
            get { return State.Name; }
        }

        public List<IResource> GetResources()
        {
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/resources/list");
            return restItems.Select(restItem => new Resource(Headers, restItem)).Cast<IResource>().ToList();
        }

        public IResource GetResource(string name)
        {
            var restItems = FindRelationAndFollow("http://api.sportingsolutions.com/rels/resources/list");
            return (from restItem in restItems where restItem.Name == name select new Resource(Headers, restItem)).FirstOrDefault();
        }
    }
}
