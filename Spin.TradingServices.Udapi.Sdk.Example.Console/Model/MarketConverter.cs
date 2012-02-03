using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Converters;

namespace Spin.TradingServices.Udapi.Sdk.Example.Console.Model
{
    public class MarketConverter : CustomCreationConverter<Market>
    {
        public override Market Create(Type objectType)
        {
            return new Market();
        }
    }
}
