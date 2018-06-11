using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    internal class ChangeConnectionMessage
    {
        public IModel NewSlaveModel;
        public bool isChangeMaster = false;
    }
}
