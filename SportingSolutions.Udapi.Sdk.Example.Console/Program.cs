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
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using NConsoler;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SportingSolutions.Udapi.Sdk.Example.Console.Model;
using SportingSolutions.Udapi.Sdk.Interfaces;

namespace SportingSolutions.Udapi.Sdk.Example.Console
{
    public class Program
    {
        private static string _url;
        private static int _echoInterval;
        private static int _echoMaxDelay;

        static void Main(string[] args)
        {
            _url = ConfigurationManager.AppSettings["url"];
            _echoInterval = Convert.ToInt32(ConfigurationManager.AppSettings["echoInterval"]);
            _echoMaxDelay = Convert.ToInt32(ConfigurationManager.AppSettings["echoMaxDelay"]);
            log4net.Config.XmlConfigurator.Configure();
            Consolery.Run(typeof(Program), args);
        }

        [Action]
        public static void Test(string userName, string password, string sport)
        {
            ICredentials credentials = new Credentials { UserName = userName, Password = password };
            var theSession = SessionFactory.CreateSession(new Uri(_url), credentials);
            //Get the Unified Data Service

           
            var theService = theSession.GetService("UnifiedDataAPI");

            var theFeature = theService.GetFeature(sport);
            //Get all the Resources (Fixtures) for the given sport
            var theResources = theFeature.GetResources();
            //Grab the first one, this is only an example after all
            var theResource = theResources.First();

            //Get the snapshot
            var theSnapshot = theResource.GetSnapshot();
           

            var fixtureSnapshot = JsonConvert.DeserializeObject<Fixture>(theSnapshot, new JsonSerializerSettings { Converters = new List<JsonConverter> { new IsoDateTimeConverter() }, NullValueHandling = NullValueHandling.Ignore });

            System.Console.WriteLine(theSnapshot);
            theResource.StreamConnected += (sender, args) => System.Console.WriteLine("Stream Connected");
            theResource.StreamEvent += (sender, args) => System.Console.WriteLine(args.Update);
            theResource.StreamDisconnected += (sender, args) => System.Console.WriteLine("Stream Disconnected");
            theResource.StreamSynchronizationError += (sender, args) => System.Console.WriteLine("Stream broken");
            //Start Streaming
            theResource.StartStreaming(_echoInterval, _echoMaxDelay);

            //Wait 60 seconds, then stop the Stream
            Thread.Sleep(600000);
            theResource.StopStreaming();
        }
    }
}
