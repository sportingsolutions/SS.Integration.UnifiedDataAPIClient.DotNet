using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spin.TradingServices.Udapi.Sdk.Example.Console.Model
{
    public class Fixture
    {
        public Fixture()
        {
            Tags = new Dictionary<string, object>();
            Markets = new List<Market>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string SoFar { get; set; }

        public string Status { get; set; }

        public string Date { get; set; }

        public Dictionary<string, object> Tags { get; set; }

        public List<Market> Markets { get; set; }
    }
}
