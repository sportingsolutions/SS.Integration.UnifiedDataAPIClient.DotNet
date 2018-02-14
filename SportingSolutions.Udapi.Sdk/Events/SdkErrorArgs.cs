using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SportingSolutions.Udapi.Sdk.Events
{
    public class SdkErrorArgs
    {
        public string ErrorMessage { get; private set; }
        public bool ShouldSuspend { get; private set; }

        public SdkErrorArgs(string errorMessage, bool shouldSuspend)
        {
            ErrorMessage = errorMessage;
            ShouldSuspend = shouldSuspend;
        }
    }
}
