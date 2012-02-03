using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Converters;

namespace Spin.TradingServices.Udapi.Sdk.Example.Console.Model
{
    public class SelectionConverter : CustomCreationConverter<Selection>
    {
        public override Selection Create(Type objectType)
        {
            return new Selection();
        }
    }
}
