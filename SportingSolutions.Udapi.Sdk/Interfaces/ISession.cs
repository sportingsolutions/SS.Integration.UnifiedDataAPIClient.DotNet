using System.Collections.Generic;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface ISession
    {
        IService GetService(string name);
        IList<IService> GetServices();
    }
}
