using Spin.TradingServices.Udapi.Sdk.Interfaces;

namespace Spin.TradingServices.Udapi.Sdk
{
    public class Credentials : ICredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
