using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class NewSubscriberMessage
    {
        public IStreamSubscriber Subscriber { get; internal set; }
		//public IConsumer Consumer { get; set; }
    }
}
