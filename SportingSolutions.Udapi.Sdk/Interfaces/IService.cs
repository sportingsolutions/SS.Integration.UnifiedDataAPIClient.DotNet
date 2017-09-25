//Copyright 2012 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System.Collections.Generic;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IService
    {
        /// <summary>
        ///     Service's name
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Interval in seconds at which the service cache will be invalidated
        /// </summary>
        int ServiceCacheInvalidationInterval { get; set; }

        /// <summary>
        ///     Property to enable/disable service cache
        /// </summary>
        bool IsServiceCacheEnabled { get; set; }

        /// <summary>
        ///     Returns a list of the features
        ///     for this service
        /// </summary>
        /// <returns></returns>
        List<IFeature> GetFeatures();

        /// <summary>
        ///     Returns the feature associated
        ///     to the given name.
        ///
        ///     It returns null if the feature
        ///     doesn't exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IFeature GetFeature(string name);
    }
}
