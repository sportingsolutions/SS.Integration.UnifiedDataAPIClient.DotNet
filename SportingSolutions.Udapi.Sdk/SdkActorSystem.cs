using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Event;
using SportingSolutions.Udapi.Sdk.Actors;

namespace SportingSolutions.Udapi.Sdk
{
    public class SdkActorSystem
    {
        private static ActorSystem _actorSystem = ActorSystem.Create("SDKSystem");

        private const string UserSystemPath = "/user/";

        internal static bool InitializeActors { get; set; } = true;
        
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
        public static void Init()
        {
            if (InitializeActors)
            {
                var dispatcher = ActorSystem.ActorOf(
                    Props.Create(() => new UpdateDispatcherActor()),
                    UpdateDispatcherActor.ActorName);
                ActorSystem.ActorOf(
                    Props.Create(() => new StreamControllerActor(dispatcher)).WithMailbox("streamcontrolleractor-mailbox"),
                    StreamControllerActor.ActorName);
                ActorSystem.ActorOf(
                    Props.Create(() => new EchoControllerActor()),
                    EchoControllerActor.ActorName);

                FaultControllerActorRef = ActorSystem.ActorOf(
                    Props.Create(() =>new FaultControllerActor()),
                    FaultControllerActor.ActorName);

                // Setup an actor that will handle deadletter type messages
                var deadletterWatchMonitorProps = Props.Create(() => new SdkDeadletterMonitorActor());
                var deadletterWatchActorRef =
                    _actorSystem.ActorOf(deadletterWatchMonitorProps, "SdkDeadletterMonitorActor");

                // subscribe to the event stream for messages of type "DeadLetter"
                _actorSystem.EventStream.Subscribe(deadletterWatchActorRef, typeof(DeadLetter));
            }
        }

        public static ActorSystem ActorSystem
        {
            get => _actorSystem;
            internal set => _actorSystem = value;
        }
    }
}
