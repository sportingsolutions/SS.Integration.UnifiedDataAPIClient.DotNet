using System.Collections.Generic;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IFeature
    {
        string Name { get; }

        List<IResource> GetResources();
        IResource GetResource(string name);
    }
}
