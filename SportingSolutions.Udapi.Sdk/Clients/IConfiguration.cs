using System;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public interface IConfiguration
    {
        Uri BaseUrl { get; }
        string ContentType { get; }
        int Timeout { get; }
        bool Compression { get; }
    }
}
