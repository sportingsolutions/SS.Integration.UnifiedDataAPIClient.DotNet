using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model
{
    public class FixtureStreamUpdate : IFixtureUpdate
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public bool IsEcho { get; set; }
    }
}
