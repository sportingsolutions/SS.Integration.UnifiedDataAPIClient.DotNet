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

        static void Main(string[] args)
        {
            _url = ConfigurationManager.AppSettings["url"];
            Consolery.Run(typeof(Program), args);
        }

        [Action]
        public static void Test(string userName, string password)
        {
            ICredentials credentials = new Credentials { UserName = userName, Password = password };
            var theSession = SessionFactory.CreateSession(new Uri(_url), credentials);
            var theService = theSession.GetService("UnifiedDataAPI");
            var theFeature = theService.GetFeature("Tennis");
            var theResources = theFeature.GetResources();
            var theResource = theResources.First();
            var theSnapshot = theResource.GetSnapshot();

            var fixtureSnapshot = JsonConvert.DeserializeObject<Fixture>(theSnapshot, new JsonSerializerSettings { Converters = new List<JsonConverter> { new IsoDateTimeConverter() }, NullValueHandling = NullValueHandling.Ignore });
            System.Console.WriteLine(theSnapshot);
            theResource.StreamConnected += (sender, args) => System.Console.WriteLine("Stream Connected");
            
            theResource.StreamEvent += (sender, args) => System.Console.WriteLine(args.Update);
            theResource.StreamDisconnected += (sender, args) => System.Console.WriteLine("Stream Disconnected");
            theResource.StartStreaming();

            Thread.Sleep(600000);
            theResource.StopStreaming();
           
        }
    }
}
