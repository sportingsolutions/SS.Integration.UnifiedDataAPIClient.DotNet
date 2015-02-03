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
using System.Linq;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public sealed class StreamController
    {
        private readonly ILog _logger;

        private static volatile StreamController _instance;
        private static readonly object InitSync = new Object();
        private static readonly object SyncRoot = new Object();
        private static readonly object ConnectSync = new Object();

        private IEchoController _echoController;
        private IConnectClient _connectClient;
        private Uri _echoUri;

        private ConnectionFactory _connectionFactory;
        private IConnection _streamConnection;

        private bool _shutdownRequested;

        private StreamController()
        {
            _logger = LogManager.GetLogger(typeof(StreamController));
            _connectionFactory = new ConnectionFactory();
        }

        public static StreamController Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new StreamController();
                        }
                    }
                }
                return _instance;
            }
        }

        public IResource CreateResource(RestItem resourceRestItem, IConnectClient connectClient)
        {
            if (_connectClient == null)
            {
                _connectClient = connectClient;
            }

            if (_echoUri == null)
            {
                var theLink = resourceRestItem.Links.First(restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/stream/batchecho");
                var theUrl = theLink.Href;
                _echoUri = new Uri(theUrl);
            }

            var resource = new Resource(resourceRestItem, connectClient, this);

            return resource;
        }

        public IModel GetStreamChannel(string host, int port, string user, string password, string virtualHost)
        {
            IModel channel = null;
            lock (ConnectSync)
            {
                if (_streamConnection == null || !_streamConnection.IsOpen)
                {
                    _connectionFactory = new ConnectionFactory
                                             {
                                                 RequestedHeartbeat = 5,
                                                 HostName = host,
                                                 Port = port,
                                                 UserName = user,
                                                 Password = password,
                                                 VirtualHost = virtualHost
                                             };

                    TryToConnect();
                }

                channel = _streamConnection.CreateModel();
            }
            return channel;
        }

        public void ShutdownConnection()
        {
            _shutdownRequested = true;
            _streamConnection.Close();
        }

        private void TryToConnect()
        {
            while (_streamConnection == null || !_streamConnection.IsOpen)
            {
                try
                {
                    _streamConnection = _connectionFactory.CreateConnection();
                    _streamConnection.ConnectionShutdown += StreamConnectionConnectionShutdown;
                    _logger.Info("Successfully connected to Streaming Server");
                }
                catch (BrokerUnreachableException buex)
                {
                    _logger.Error("Unable to connect to streaming server...Retrying", buex);
                    Thread.Sleep(100);
                }
            }
        }

        private void StreamConnectionConnectionShutdown(IConnection connection, ShutdownEventArgs reason)
        {
            if (!_shutdownRequested)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("There has been a streaming connection failure").AppendLine();
                stringBuilder.Append(reason);

                _logger.Error(stringBuilder.ToString());
                TryToConnect();    
            }
            else
            {
                _shutdownRequested = false;
            }
        }

        public void StartEcho(string virtualHost, int echoInterval)
        {
            lock (InitSync)
            {
                if (_echoController == null)
                {
                    _echoController = new EchoController(_connectClient, _echoUri);
                    _echoController.StartEchos(virtualHost, echoInterval);
                }
            }
        }

        public void StopEcho()
        {
            lock (InitSync)
            {
                if (_echoController != null)
                {
                    _echoController.StopEchos();
                    _echoController = null;
                }
            }
        }
    }
}
