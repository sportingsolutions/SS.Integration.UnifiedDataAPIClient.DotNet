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
    public class Configuration : IConfiguration
    {
        private const string DEFAULT_CONTENT_TYPE = "application/json";
        private const bool DEFAULT_USE_COMPRESSION = true;
        private const bool DEFAULT_USE_ECHOS = true;
        private const int DEFAULT_TIMEOUT_MILLISECONDS = 60000;
        private const int DEFAULT_WAIT_INTERVAL_ECHOS_MILLISECONDS = 10000;
        private const int DEFAULT_MISSED_ECHOS = 3;
        private const bool DEFAULT_VERBOSE_LOGGING = true;
        private const ushort DEFAULT_AMQP_HEARTBEAT = 15;
        private bool DEFAULT_ENABLE_AUTO_RECONNECT = true;
        private int DEFAULT_DISCONNECTION_DELAY = 10;
        private const bool DEFAULT_USE_SINGLE_QUEUE_STREAMING_METHOD = false;

        static Configuration()
        {
            Instance = new Configuration();
        }

        protected Configuration()
        {
            Compression = DEFAULT_USE_COMPRESSION;
            ContentType = DEFAULT_CONTENT_TYPE;
            Timeout = DEFAULT_TIMEOUT_MILLISECONDS;
            UseEchos = DEFAULT_USE_ECHOS;
            MissedEchos = DEFAULT_MISSED_ECHOS;
            EchoWaitInterval = DEFAULT_WAIT_INTERVAL_ECHOS_MILLISECONDS;
            VerboseLogging = DEFAULT_VERBOSE_LOGGING;
            AMQPMissedHeartbeat = DEFAULT_AMQP_HEARTBEAT;
            AutoReconnect = DEFAULT_ENABLE_AUTO_RECONNECT;
            DisconnectionDelay = DEFAULT_DISCONNECTION_DELAY;
            UseSingleQueueStreamingMethod = DEFAULT_USE_SINGLE_QUEUE_STREAMING_METHOD;
        }

        public static IConfiguration Instance { get; private set; }

        public int Timeout { get; set; }

        public string ContentType { get; set; }

        public bool Compression { get; set; }

        public bool UseEchos { get; set; }

        public int MissedEchos { get; set; }

        public int EchoWaitInterval { get; set; }

        public bool VerboseLogging { get; set; }

        public ushort AMQPMissedHeartbeat { get; set; }

        public bool AutoReconnect { get; set; }

        public int DisconnectionDelay { get; set; }

        public bool UseSingleQueueStreamingMethod { get; set; }
    }
}
