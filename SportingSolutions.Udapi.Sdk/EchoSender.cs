using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public sealed class EchoSender
    {
        private readonly ILog _logger;

        private static volatile EchoSender _instance;
        private static readonly object SyncRoot = new Object();
        private static readonly object InitSync = new Object();

        private IConnectClient _connectClient;

        private Timer _echoTimer;
        private Uri _echoUri;
        private Guid _lastSentGuid;
        private string _virtualHost;

        private EchoSender()
        {
            _logger = LogManager.GetLogger(typeof(EchoSender));
        }

        public static EchoSender Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new EchoSender();
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
                var theLink =
                        resourceRestItem.Links.First(
                            restLink => restLink.Relation == "http://api.sportingsolutions.com/rels/stream/batchecho");
                var theUrl = theLink.Href;
                _echoUri = new Uri(theUrl);
            }

            var resource = new Resource(resourceRestItem, connectClient, this);
            
            return resource;
        }

        public void StartEcho(string virtualHost, int echoInterval)
        {
            lock (InitSync)
            {
                if (_echoTimer == null)
                {
                    _virtualHost = virtualHost;
                    _echoTimer = new Timer(x => SendEcho(), null, 0, echoInterval);
                }
            }
        }

        private void SendEcho()
        {
            try
            {
                _lastSentGuid = Guid.NewGuid();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var streamEcho = new StreamEcho
                {
                    Host = _virtualHost,
                    Message = _lastSentGuid.ToString() + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                var response = _connectClient.Request(_echoUri, Method.POST, streamEcho, "application/json", 3000);

                if (response == null)
                {
                    _logger.WarnFormat("Post Echo had null response and took duration={0}ms", stopwatch.ElapsedMilliseconds);

                }
                else if (response.ErrorException != null)
                {
                    RestErrorHelper.LogRestError(_logger, response, string.Format("Echo Http Error took {0}ms", stopwatch.ElapsedMilliseconds));
                }
                else
                {
                    _logger.DebugFormat("Post Echo took duration={0}ms", stopwatch.ElapsedMilliseconds);   
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Unable to post echo",ex);
            }
        }
    }
}
