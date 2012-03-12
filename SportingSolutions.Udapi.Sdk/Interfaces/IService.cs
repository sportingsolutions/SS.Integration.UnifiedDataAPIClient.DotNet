using System.Collections.Generic;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IService
    {
        string Name { get; }

        List<IFeature> GetFeatures();
        IFeature GetFeature(string name);
    }
}
