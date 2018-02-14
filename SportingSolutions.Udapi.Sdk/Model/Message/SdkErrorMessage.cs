using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.Model.Message
{
    public class SdkErrorMessage
    {
        public string Message { get; set; }
        public bool IsSuspend { get; set; }
    }
}
