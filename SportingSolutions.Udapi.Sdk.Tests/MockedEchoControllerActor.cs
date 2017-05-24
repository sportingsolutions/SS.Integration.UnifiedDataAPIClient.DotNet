using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk.Actors;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    internal class MockedEchoControllerActor : EchoControllerActor
    {
        public override void AddConsumer(IStreamSubscriber subscriber)
        {
            base.AddConsumer(subscriber);
        }
    }
}
