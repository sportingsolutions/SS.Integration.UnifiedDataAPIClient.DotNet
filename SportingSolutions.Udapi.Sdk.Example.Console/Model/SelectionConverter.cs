using System;
using Newtonsoft.Json.Converters;

namespace SportingSolutions.Udapi.Sdk.Example.Console.Model
{
    public class SelectionConverter : CustomCreationConverter<Selection>
    {
        public override Selection Create(Type objectType)
        {
            return new Selection();
        }
    }
}
