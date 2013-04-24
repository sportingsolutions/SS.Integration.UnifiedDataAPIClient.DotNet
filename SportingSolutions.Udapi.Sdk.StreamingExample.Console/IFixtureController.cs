using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    public interface IFixtureController
    {
        void RemoveFixture(string id);
        void RestartFixture(string id);
        void StopFixture(string id);

        bool Contains(string id);
    }
}
