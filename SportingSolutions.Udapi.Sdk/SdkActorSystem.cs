using Akka.Actor;
using SportingSolutions.Udapi.Sdk.Actors;
using SingleQueue = SportingSolutions.Udapi.Sdk.Actors.SingleQueue;

namespace SportingSolutions.Udapi.Sdk
{
    public class SdkActorSystem
    {
        private static ActorSystem _actorSystem = ActorSystem.Create("SDKSystem");

        private const string UserSystemPath = "/user/";

        public static string UpdateDispatcherPath = UserSystemPath +
                                                    (UDAPI.Configuration.UseSingleQueueStreamingMethod
                                                        ? SingleQueue.UpdateDispatcherActor.ActorName
                                                        : UpdateDispatcherActor.ActorName);

        public static string StreamControllerActorPath = UserSystemPath +
                                                         (UDAPI.Configuration.UseSingleQueueStreamingMethod
                                                             ? SingleQueue.StreamControllerActor.ActorName
                                                             : StreamControllerActor.ActorName);

        public static string EchoControllerActorPath = UserSystemPath +
                                                       (UDAPI.Configuration.UseSingleQueueStreamingMethod
                                                           ? SingleQueue.EchoControllerActor.ActorName
                                                           : EchoControllerActor.ActorName);

        public static Props BuildUpdateDispatcherActor = Props.Create<UpdateDispatcherActor>();
        public static Props BuildUpdateDispatcherSingleQueueActor = Props.Create<SingleQueue.UpdateDispatcherActor>();


        static SdkActorSystem()
        {

        }

        /// <summary>
        /// Actor system shouldn't be provided unless your implemention specifically requires it
        /// </summary>
        /// <param name="actorSystem"></param>
        public static void Init(ActorSystem actorSystem = null, bool initialiseActors = true)
        {
            _actorSystem = actorSystem ?? _actorSystem;

            if (initialiseActors)
            {
                if (UDAPI.Configuration.UseSingleQueueStreamingMethod)
                {
                    var dispatcher = ActorSystem.ActorOf(
                        BuildUpdateDispatcherSingleQueueActor,
                        SingleQueue.UpdateDispatcherActor.ActorName);

                    ActorSystem.ActorOf(
                        Props.Create(() => new SingleQueue.StreamControllerActor(dispatcher)),
                        SingleQueue.StreamControllerActor.ActorName);
                    ActorSystem.ActorOf(
                        Props.Create(() => new SingleQueue.EchoControllerActor()),
                        SingleQueue.EchoControllerActor.ActorName);
                }
                else
                {
                    var dispatcher = ActorSystem.ActorOf(
                        BuildUpdateDispatcherActor,
                        UpdateDispatcherActor.ActorName);

                    ActorSystem.ActorOf(
                        Props.Create(() => new StreamControllerActor(dispatcher)),
                        StreamControllerActor.ActorName);
                    ActorSystem.ActorOf(
                        Props.Create(() => new EchoControllerActor()),
                        EchoControllerActor.ActorName);
                }
            }
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}
