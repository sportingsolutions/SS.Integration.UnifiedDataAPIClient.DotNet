using System;
using System.Configuration;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Configuration
{
    public class Settings : ISettings
    {
        public readonly static ISettings Instance = new Settings();

        private Settings()
        {
            User = ConfigurationManager.AppSettings["user"];
            Password = ConfigurationManager.AppSettings["password"];
            Url = ConfigurationManager.AppSettings["url"];

            var value = ConfigurationManager.AppSettings["newFixtureCheckerFrequency"];
            FixtureCheckerFrequency = value == "" ? 60000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["startingRetryDelay"];
            StartingRetryDelay = value == "" ? 500 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["maxRetryDelay"];
            MaxRetryDelay = value == "" ? 65000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["maxRetryAttempts"];
            MaxRetryAttempts = value == "" ? 3 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["echoInterval"];
            EchoInterval = value == "" ? 10000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["echoMaxDelay"];
            EchoMaxDelay = value == "" ? 3000 : Convert.ToInt32(value);
        }

        public string User { get; private set; }
        public string Password { get; private set; }
        public string Url { get; private set; }
        public int FixtureCheckerFrequency { get; private set; }
        public int StartingRetryDelay { get; private set; }
        public int MaxRetryDelay { get; private set; }
        public int MaxRetryAttempts { get; private set; }
        public int EchoInterval { get; private set; }
        public int EchoMaxDelay { get; private set; }
    }
}
