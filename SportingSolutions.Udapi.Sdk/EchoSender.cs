using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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

        private static IObserver<string> _echoListener;
        private static List<string> _recordedEchoes = new List<string>();
        private static List<string> _receivedEchoes = new List<string>();
        private static IObservable<IList<string>> _echoesToValidate;

        public static void StopEcho()
        {
            lock (_sync)
            {
                _echoTimer.Dispose();
            }
        }

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
                    _echoListener = Observer.Create<string>(ValidateEcho);

                    StreamSubscriber.SubscribeToEchoStream(_echoListener);

                    GetRecordedEchoes().ToObservable().Buffer(TimeSpan.FromSeconds(20)).SubscribeOn(Scheduler.Default)
                        .Subscribe(ValidateEchoes, () => _logger.Error("that's the end of echoes"));

                    _logger.Debug("non blocking!");

                    //_recordedEchoes.ToObservable().Buffer(TimeSpan.FromSeconds(20)).SubscribeOn(Scheduler.Default).Subscribe(x=> 

                    //    );
                }
            }
        }

        private static IEnumerable<string> GetRecordedEchoes()
        {
            while (true)
            {
                int i = 0;
                for (i = 0; i < _recordedEchoes.Count; i++)
                    yield return _recordedEchoes[i];

                lock (_sync)
                    if (_recordedEchoes.Count > 0)
                        _recordedEchoes.RemoveRange(0, i);
            }
        }

        private static void ValidateEchoes(IList<string> sentEchoesInTheLastXs)
        {
            var receivedEchoes = _receivedEchoes.ToArray();
            _logger.Info("Validation in progress");

            if (!sentEchoesInTheLastXs.All(x => receivedEchoes.Contains(x)))
            {
                _logger.Warn("Echo failed!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            }
            else
            {
                _logger.DebugFormat("All echoes validated successfully");
            }

            _receivedEchoes.RemoveRange(0, receivedEchoes.Length);
        }

        private static void ValidateEcho(string message)
        {
            _receivedEchoes.Add(message);

            _logger.DebugFormat("ECHO arrived {0}", message);

            var split = message.Split(';');

            var lastRecievedEchoGuid = split[0];
            var timeSent = DateTime.ParseExact(split[1], "yyyy-MM-ddTHH:mm:ss.fffZ",
                                               CultureInfo.InvariantCulture);
            var roundTripTime = DateTime.Now - timeSent;

            var roundMillis = roundTripTime.TotalMilliseconds;
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
                                Host = queueDetails.VirtualHost.Substring(1),
                                Queue = queueDetails.Name,
                                Message = echoGuid + ";" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                            };

                        lock (_sync)
                            _recordedEchoes.Add(streamEcho.Message);


                        _postEcho(streamEcho);
                        _logger.DebugFormat("Successfully sent echo");

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
