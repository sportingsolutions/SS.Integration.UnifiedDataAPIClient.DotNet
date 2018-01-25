using Akka.Actor;
using Akka.Event;
using SportingSolutions.Udapi.Sdk.Actors;

namespace SportingSolutions.Udapi.Sdk
{
    public class SdkActorSystem
    {
        private static ActorSystem _actorSystem = ActorSystem.Create("SDKSystem");

        private const string UserSystemPath = "/user/";
        
        public const string UpdateDispatcherPath = UserSystemPath + UpdateDispatcherActor.ActorName;
        public const string StreamControllerActorPath = UserSystemPath +StreamControllerActor.ActorName;
        public const string EchoControllerActorPath = UserSystemPath + EchoControllerActor.ActorName;

        public static Props BuildUpdateDispatcherActor = Props.Create<UpdateDispatcherActor>();

        
        static SdkActorSystem()
        {
          
        }
        
        /// <summary>
        /// Actor system shouldn't be provided unless your implemention specifically requires it
        /// </summary>
        /// <param name="actorSystem"></param>
        public static void Init(ActorSystem actorSystem = null,bool initialiseActors = true)
        {
            _actorSystem = actorSystem ?? _actorSystem;

            if (initialiseActors)
            {
                var dispatcher = ActorSystem.ActorOf(BuildUpdateDispatcherActor, UpdateDispatcherActor.ActorName);
                ActorSystem.ActorOf(Props.Create<StreamControllerActor>(() => new StreamControllerActor(dispatcher)), StreamControllerActor.ActorName);
                ActorSystem.ActorOf(Props.Create(() => new EchoControllerActor()), EchoControllerActor.ActorName);
            }

            // Setup an actor that will handle deadletter type messages
            var deadletterWatchMonitorProps = Props.Create(() => new SdkDeadletterMonitorActor());
            var deadletterWatchActorRef = _actorSystem.ActorOf(deadletterWatchMonitorProps, "SdkDeadletterMonitorActor");

            // subscribe to the event stream for messages of type "DeadLetter"
            _actorSystem.EventStream.Subscribe(deadletterWatchActorRef, typeof(DeadLetter));
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}
