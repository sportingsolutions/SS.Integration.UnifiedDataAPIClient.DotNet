using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spin.TradingServices.Udapi.Sdk.Example.Console.Model
{
    public class Selection
    {
        public Selection()
        {
            Tags = new Dictionary<string, object>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public Dictionary<string, object> Tags { get; set; }

        public string DisplayPrice { get; set; }

        public double? Price { get; set; }

        public string Status { get; set; }

        public bool? Tradable { get; set; }
    }
}
