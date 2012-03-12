using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk
{
    public class Credentials : ICredentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
