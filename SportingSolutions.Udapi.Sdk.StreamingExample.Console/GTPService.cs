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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model;
using log4net;
using log4net.Appender;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    public class GTPService : IDisposable
    {
        private readonly ILog _logger;
        private Timer _theTimer;
        private readonly IList<string> _sportsList;
        private readonly ConcurrentDictionary<string, StreamListener> _listeners;
        private readonly ConcurrentDictionary<string, bool> _activeFixtures;

        public GTPService()
        {
            _logger = LogManager.GetLogger(typeof(GTPService).ToString());
            _sportsList = new List<string> {"Tennis","Football","Baseball","Basketball","IceHockey","Rugby"};
            _listeners = new ConcurrentDictionary<string, StreamListener>();
            _activeFixtures = new ConcurrentDictionary<string, bool>();
        }

        public void Start()
        {
            try
            {
                _logger.Debug("Starting GTPService");
                _logger.Info("Connecting to UDAPI....");
                ICredentials credentials = new Credentials { UserName = ConfigurationManager.AppSettings["ss.user"], Password = ConfigurationManager.AppSettings["ss.password"] };
                var theSession = SessionFactory.CreateSession(new Uri(ConfigurationManager.AppSettings["ss.url"]), credentials);
                _logger.Info("Successfully connected to UDAPI.");
                _logger.Debug("UDAPI, Getting Service");
                var theService = theSession.GetService("UnifiedDataAPI");
                _logger.Debug("UDAPI, Retrieved Service");
                _logger.Info("Starting timer...");
                _theTimer = new Timer(timerAutoEvent => TimerEvent(theService), null, 0, Convert.ToInt32(ConfigurationManager.AppSettings["ss.newFixtureCheckerFrequency"]));    
            }
            catch(Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        private void TimerEvent(IService theService)
        {
            try
            {
                foreach (var sport in _sportsList)
                {
                    var theFeature = theService.GetFeature(sport);
                    if (theFeature != null)
                    {
                        _logger.InfoFormat("Get the list of available fixtures for {0} from GTP", sport);
                        var fixtures = theFeature.GetResources();

                        if (fixtures != null && fixtures.Count > 0)
                        {
                            var tmpSport = sport;
                            Parallel.ForEach(fixtures, new ParallelOptions { MaxDegreeOfParallelism = 10 },
                                                fixture => ProcessFixture(fixture, tmpSport));
                        }
                        else
                        {
                            _logger.InfoFormat("There are currently no {0} fixtures in UDAPI", sport);
                        }
                    }
                    else
                    {
                        _logger.InfoFormat("Cannot find {0} in UDAPI....", sport);
                    }
                } 
            }
            catch (AggregateException aggex)
            {
                foreach (var innerException in aggex.InnerExceptions)
                {
                    _logger.Error(innerException);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        public static ILog AddAppender(string loggerName, IAppender appender)
        {
            var log = LogManager.GetLogger(loggerName);
            var l = (log4net.Repository.Hierarchy.Logger)log.Logger;

            l.AddAppender(appender);
            return log;
        }

        public static void SetLevel(string loggerName, string levelName)
        {
            var log = LogManager.GetLogger(loggerName);
            var l = (log4net.Repository.Hierarchy.Logger)log.Logger;

            l.Level = l.Hierarchy.LevelMap[levelName];
        }

        public static IAppender CreateRollingFileAppender(string name,string fileName)
        {
            var appender = new FileAppender();
            appender.Name = name;
            appender.File = ConfigurationManager.AppSettings["fixture.log.path"] + fileName + ".log";
            appender.AppendToFile = true;
          
            var layout = new log4net.Layout.PatternLayout();
            layout.ConversionPattern = "%date;%message%newline";
            layout.ActivateOptions();

            appender.Layout = layout;
            appender.ActivateOptions();

            return appender;
        }

        private void ProcessFixture(IResource fixture, string sport)
        {
            if (!_activeFixtures.ContainsKey(fixture.Id) && !_listeners.ContainsKey(fixture.Id))
            {
                _activeFixtures.TryAdd(fixture.Id, true);
                
                SetLevel(fixture.Id,"INFO");
                var log = AddAppender(fixture.Id,CreateRollingFileAppender(fixture.Name,fixture.Name));

                var matchStatus = 0;
                
                if (fixture.Content != null)
                {
                    matchStatus = fixture.Content.MatchStatus;
                }

                //if not match over
                if(matchStatus != 50)
                {
                    _logger.InfoFormat("Get UDAPI Snapshot for {0} id {1}", fixture.Name, fixture.Id);
                    var snapshotString = fixture.GetSnapshot();
                    _logger.InfoFormat("Successfully retrieved UDAPI Snapshot for {0} id {1}", fixture.Name, fixture.Id);

                    var fixtureSnapshot =
                       (Fixture)
                       JsonConvert.DeserializeObject(snapshotString, typeof(Fixture),
                                                       new JsonSerializerSettings
                                                       {
                                                           Converters =
                                                               new List<JsonConverter> { new IsoDateTimeConverter() },
                                                           NullValueHandling = NullValueHandling.Ignore
                                                       });

                    var epoch = fixtureSnapshot.Epoch;

                    var names = ProcessSnapshot(fixtureSnapshot);

                    var streamListener = new StreamListener(fixture, epoch, sport,log,names);
                    _listeners.TryAdd(fixture.Id, streamListener);
                }
                else
                {
                    _logger.InfoFormat("Fixture {0} id {1} has finished. Will not process", fixture.Name, fixture.Id);
                }
                bool y;
                _activeFixtures.TryRemove(fixture.Id, out y);
            }
            else
            {
                _logger.InfoFormat("Fixture {0} id {1} is currently being processed", fixture.Name, fixture.Id);
                if (_listeners.ContainsKey(fixture.Id))
                {
                    if (_listeners[fixture.Id].FixtureEnded)
                    {
                        StreamListener theListener;
                        if (_listeners.TryRemove(fixture.Id, out theListener))
                        {
                            _logger.InfoFormat("Fixture {0} id {1} is over.", fixture.Name, fixture.Id);
                        }
                    }
                }
            }
        }

        private List<Tuple<string, string, Dictionary<string, string>>> ProcessSnapshot(Fixture fixture)
        {
            var name = new List<Tuple<string, string, Dictionary<string, string>>>();
 
            foreach (var market in fixture.Markets)
            {
                Tuple<string, string, Dictionary<string, string>> t;
                var marketId = market.Id;
                var marketName = market.Id;
                if(market.Tags.ContainsKey("name"))
                {
                    marketName = market.Tags["name"].ToString();
                }
                var selections = new Dictionary<string, string>();
                foreach (var selection in market.Selections)
                {
                    var selectionId = selection.Id;
                    var selectionName = selection.Id;
                    if(selection.Tags.ContainsKey("name"))
                    {
                        selectionName = selection.Tags["name"].ToString();
                    }
                    selections.Add(selectionId,selectionName);
                }
                t = new Tuple<string, string, Dictionary<string, string>>(marketId,marketName,selections);
                name.Add(t);
            }
            return name;
        }

        public void Stop()
        {
            if (_theTimer != null)
            {
                _theTimer.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_theTimer != null)
                {
                    _theTimer.Dispose();
                    _theTimer = null;
                }
            }
        }
    }
}
