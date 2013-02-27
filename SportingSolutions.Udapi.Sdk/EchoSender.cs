using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    internal class EchoSender
    {
        private static Action<StreamEcho> _postEcho;
        private readonly string _uri;
        private static List<QueueDetails> _queues;
        private static ILog _logger = LogManager.GetLogger(typeof(EchoSender));
        
        static EchoSender()
        {
            _queues = new List<QueueDetails>();
        }

        private static object _sync = new object();
        private static Timer _echoTimer;

        public static void StartEcho(Action<StreamEcho> postEcho, QueueDetails queueDetails)
        {

            lock (_sync)
            {
                _queues.Add(queueDetails);

                if (_postEcho == null)
                    _postEcho = postEcho;

                if (_echoTimer == null)
                {
                    _echoTimer = new Timer(x => SendEcho(), null, 0, 10000);
                }
            }
        }

        internal static void SendEcho()
        {
            _queues.AsParallel().ForAll(queueDetails =>
                {
                    try
                    {
                        var echoGuid = Guid.NewGuid().ToString();

                        var streamEcho = new StreamEcho
                            {
                                Host = queueDetails.Host,
                                Queue = queueDetails.Name,
                                Message = echoGuid + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                            };

                        _postEcho(streamEcho);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            string.Format("Unable to post echo"), ex);
                    }
                });
        }
    }
}
