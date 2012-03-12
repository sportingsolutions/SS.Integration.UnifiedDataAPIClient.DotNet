using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.Model
{
    public class RestItem
    {
        public RestItem()
        {
            Links = new List<RestLink>();
        }

        public RestItem(string name)
            : this()
        {
            Name = name;
        }

        public string Name { get; set; }
        public Summary Content { get; set; }

        public List<RestLink> Links { get; set; }
    }
}
