using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public interface ICredentials
    {
        string ApiUser { get; }
        string ApiKey { get; }
    }
}
