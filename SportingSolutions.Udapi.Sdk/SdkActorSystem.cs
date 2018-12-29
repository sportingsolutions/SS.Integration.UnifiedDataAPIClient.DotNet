using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Event;
using System.Threading;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk.Actors;
using log4net;

namespace SportingSolutions.Udapi.Sdk
{
    public static class SdkActorSystem
    {
        public static ActorSystem ActorSystem { get; internal set; } = ActorSystem.Create("SDKSystem");


        private static readonly ILog Logger = LogManager.GetLogger(typeof(SdkActorSystem));

        private const string UserSystemPath = "/user/";

        public static bool InitializeActors { get; set; } = true;
        
        public static readonly string UpdateDispatcherPath = UserSystemPath + UpdateDispatcherActor.ActorName;
        public static readonly string StreamControllerActorPath = UserSystemPath + StreamControllerActor.ActorName;
        public static readonly string EchoControllerActorPath = UserSystemPath + EchoControllerActor.ActorName;
        public static readonly string FaultControllerActorPath = UserSystemPath + FaultControllerActor.ActorName;


        public static ICanTell FaultControllerActorRef { set; get; }

        static SdkActorSystem()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public static void Init(bool needToRecreateActorSystem = false)
        {
            if (InitializeActors)
            {
                if (needToRecreateActorSystem)
                {
                    if (SdkActorSystem.ActorSystem != null)
                        TerminateActorSystem();

                    ActorSystem = ActorSystem.Create("SDKSystem");
                    Logger.Debug($"UDAPI ActorSystem is created, creating actors...");
                }

                var dispatcher = ActorSystem.ActorOf(
                    Props.Create(() => new UpdateDispatcherActor()),
                    UpdateDispatcherActor.ActorName);

               ActorSystem.ActorOf(
                    Props.Create(() => new StreamControllerActor(dispatcher)),
                    StreamControllerActor.ActorName);
                
                ActorSystem.ActorOf(
                    Props.Create(() => new EchoControllerActor()).WithMailbox("echocontrolleractor-mailbox"),
                    EchoControllerActor.ActorName);

                FaultControllerActorRef = ActorSystem.ActorOf(
                    Props.Create(() =>new FaultControllerActor()),
                    FaultControllerActor.ActorName);

                var deadletterWatchActorRef = ActorSystem.ActorOf(
                    Props.Create(() => new SdkDeadletterMonitorActor()),
                    "SdkDeadletterMonitorActor");

                // subscribe to the event stream for messages of type "DeadLetter"
                ActorSystem.EventStream.Subscribe(deadletterWatchActorRef, typeof(DeadLetter));

                InitializeActors = false;
            }
        }

        private static void TerminateActorSystem()
        {
            Logger.Debug("Terminating UDAPI ActorSystem...");
            SdkActorSystem.ActorSystem.Terminate().Wait();
            Logger.Debug("UDAPI ActorSystem has been terminated");
        }
    }
}
