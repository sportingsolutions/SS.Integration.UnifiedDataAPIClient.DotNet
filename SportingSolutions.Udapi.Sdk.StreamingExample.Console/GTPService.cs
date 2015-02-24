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
using System.Threading;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Configuration;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    public class GTPService : IDisposable
    {
        private readonly ILog _logger;
        private Timer _theTimer;
        private readonly ISettings _settings;
        private readonly IList<string> _sportsList;
        private readonly ConcurrentDictionary<string, StreamListener> _listeners;
        private readonly ConcurrentDictionary<string, bool> _activeFixtures;


        public GTPService(ISettings settings = null)
        {
            _settings = settings ?? Settings.Instance;
            _logger = LogManager.GetLogger(typeof(GTPService).ToString());
            _sportsList = new List<string> {"Football"};
            _listeners = new ConcurrentDictionary<string, StreamListener>();
            _activeFixtures = new ConcurrentDictionary<string, bool>();
        }

        public void Start()
        {
            try
            {
                _logger.Debug("Starting GTPService");

                IService theService = new Udapi.Udapi();

                _logger.Info("Starting timer...");
                _theTimer = new Timer(timerAutoEvent => TimerEvent(theService), null, 0, _settings.FixtureCheckerFrequency);    
            }
            catch(Exception ex)
            {
                _logger.Fatal("A fatal error has occurred and the EVenue Adapater cannot start. You can try a manual restart", ex);   
                throw;
            }
        }

        private void TimerEvent(IService theService)
        {
            if (FixtureController.IsAddingItems)
                return;

            try
            {
                FixtureController.BeginAddingItems();
                Parallel.ForEach(_sportsList, new ParallelOptions { MaxDegreeOfParallelism = 10 },
                                 sport =>
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
                                 });
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
            finally
            {
                FixtureController.FinishAddingItems();
            }
        }

        private void ProcessFixture(IResource fixture, string sport)
        {
            if (fixture.Id == "cHr2GRRjCB9D7yXi4FoEQ2bt-KI")
                return;

            if (!FixtureController.Contains(fixture.Id))
            {
                try
                {
                    var matchStatus = 0;
                    
                    if (fixture.Content != null)
                        matchStatus = fixture.Content.MatchStatus;

                    //if not match over
                    if (matchStatus != (int)SSMatchStatus.MatchOver)
                    {
                        _logger.InfoFormat("Get UDAPI Snapshot for {0} id {1}", fixture.Name, fixture.Id);
                        var snapshotString = fixture.GetSnapshot();
                        _logger.InfoFormat("Successfully retrieved UDAPI Snapshot for {0} id {1}", fixture.Name, fixture.Id);

                        var fixtureSnapshot = snapshotString.FromJson<Fixture>();
                        var epoch = fixtureSnapshot.Epoch;

                        //process the snapshot here and push it into the client system

                        try
                        {
                            FixtureController.AddListener(fixture.Id, () => new StreamListener(fixture, epoch));
                        }
                        catch (Exception)
                        {
                            _logger.ErrorFormat("sport=\"{0}\" : fixtureName=\"{1}\" - Unable to stream fixture", sport, fixture.Name);
                            throw;
                        }
                        
                    }
                    else
                    {
                        _logger.InfoFormat("Fixture {0} id {1} has finished. Will not process", fixture.Name, fixture.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("Fixture {0} id {1} There is a problem processing this fixture", fixture.Name, fixture.Id), ex);
                }
            }
            else
            {
                bool isMatchOver = FixtureController.Contains(fixture.Id) && FixtureController.GetItem(fixture.Id).FixtureEnded;

                if (isMatchOver)
                {
                    if (FixtureController.Contains(fixture.Id))
                    {
                        FixtureController.RemoveFixture(fixture.Id);
                        _logger.InfoFormat("fixtureName=\"{0}\" fixtureId={1} is over.", fixture.Name, fixture.Id);
                    }
                }
                else
                {
                    _logger.InfoFormat("fixtureName=\"{0}\" fixtureId={1} is currently being processed", fixture.Name, fixture.Id);
                }

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

                        bool activeFixture;
                        _activeFixtures.TryRemove(fixture.Id, out activeFixture);
                    }
                }
            }
        }

        public void Stop()
        {
            if (_theTimer != null)
            {
                _theTimer.Dispose();
                _theTimer = null;

                if(_listeners != null)
                {
                    Parallel.ForEach(_listeners.Keys, new ParallelOptions {MaxDegreeOfParallelism = 10},
                                     theKey =>
                                         {
                                             var listener = _listeners[theKey];
                                             listener.StopListening();
                                         });
                }
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
