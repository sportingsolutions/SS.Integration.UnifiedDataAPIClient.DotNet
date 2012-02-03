using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spin.TradingServices.Udapi.Sdk.Model
{
    public class RestLink
    {
        public string Relation { get; set; }
        public string Href { get; set; }
        public string[] Verbs { get; set; }

        public RestLink()
        {
        }

        public RestLink(string relation, string href, string[] verbs, bool sign = false)
        {
            Relation = relation;
            Href = href;
            Verbs = verbs;
        }
    }
}
