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

        /// <summary>
        ///     True if echos should be used
        /// </summary>
        bool UseEchos { get; }

        /// <summary>
        ///     Number of missed echos before
        ///     disconnecting a resorce
        /// </summary>
        int MissedEchos { get; }

        /// <summary>
        ///     Milliseconds between each batchecho
        /// </summary>
        int EchoWaitInterval { get; }

        /// <summary>
        ///     True to enable verbose logging
        /// </summary>
        bool VerboseLogging { get; }


        ushort AMQPMissedHeartbeat { get; }

        /// <summary>
        /// Enables auto reconnection on RabbitMQ.Client
        /// </summary>
        bool AutoReconnect { get;  }

        int DisconnectionDelay { get; }
    }
}
