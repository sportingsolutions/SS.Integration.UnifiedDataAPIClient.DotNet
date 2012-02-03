
using System.Collections.Generic;

namespace Spin.TradingServices.Udapi.Sdk.Interfaces
{
    public interface ISession
    {
        IService GetService(string name);
        IList<IService> GetServices();
    }
}
