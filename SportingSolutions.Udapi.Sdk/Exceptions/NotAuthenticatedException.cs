using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.Exceptions
{
    public class NotAuthenticatedException : Exception
    {
        public NotAuthenticatedException(string message, Exception innerException):base(message,innerException)
        {
            
        }
    }
}
