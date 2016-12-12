using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
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

        //public static Props BuildStreamControllerActor =
        //    Props.Create<StreamControllerActor>(() => new StreamControllerActor(new UpdateDispatcher()));
        

        static SdkActorSystem()
        {
            //_actorSystem = ActorSystem.Create("SDKSystem");
            
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
        }

        public static ActorSystem ActorSystem => _actorSystem;
    }
}
