using System;
using Newtonsoft.Json.Converters;

namespace SportingSolutions.Udapi.Sdk.Example.Console.Model
{
    public class MarketConverter : CustomCreationConverter<Market>
    {
        public override Market Create(Type objectType)
        {
            return new Market();
        }
    }
}
