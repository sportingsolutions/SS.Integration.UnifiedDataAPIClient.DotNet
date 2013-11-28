using System;
using System.Diagnostics;
using System.Threading;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public class EchoController : IEchoController
    {
        private readonly ILog _logger;
        private static readonly object InitSync = new Object();
        private Timer _echoTimer;
        private Guid _lastSentGuid;
        private readonly IConnectClient _connectClient;
        private readonly Uri _echoUri;

        public EchoController(IConnectClient connectClient, Uri echoUri)
        {
            _logger = LogManager.GetLogger(typeof(EchoController));
            _connectClient = connectClient;
            _echoUri = echoUri;
        }

        public void StartEchos(string virtualHost, int echoInterval)
        {
            lock (InitSync)
            {
                if (_echoTimer == null)
                {
                    _echoTimer = new Timer(x => SendEcho(_echoUri, virtualHost), null, 0, echoInterval);
                }
            }
        }

        public void StopEchos()
        {
            _echoTimer.Dispose();
            _echoTimer = null;
        }

        private void SendEcho(Uri echoUri, string virtualHost)
        {
            try
            {
                _lastSentGuid = Guid.NewGuid();

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var streamEcho = new StreamEcho
                {
                    Host = virtualHost,
                    Message = _lastSentGuid.ToString() + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                var response = _connectClient.Request(echoUri, Method.POST, streamEcho, "application/json", 3000);

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
                _logger.Error("Unable to post echo", ex);
            }
        }
    }
}
