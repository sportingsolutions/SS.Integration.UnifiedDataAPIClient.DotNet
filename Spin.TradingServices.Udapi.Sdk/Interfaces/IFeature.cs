using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spin.TradingServices.Udapi.Sdk.Interfaces
{
    public interface IFeature
    {
        string Name { get; }

        List<IResource> GetResources();
        IResource GetResource(string name);
    }
}
