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

using System;

namespace SportingSolutions.Udapi.Sdk.Clients
{
    public interface IConfiguration
    {
        /// <summary>
        ///     Service base URL
        /// </summary>
        Uri BaseUrl { get; }

        /// <summary>
        ///     Http request content type
        /// </summary>
        string ContentType { get; }

        /// <summary>
        ///     Request timeout in milliseconds
        /// </summary>
        int Timeout { get; }

        /// <summary>
        ///     True if http compression should be used
        /// </summary>
        bool Compression { get; }
    }
}
