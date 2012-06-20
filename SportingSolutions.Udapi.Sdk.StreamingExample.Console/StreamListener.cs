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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    public class StreamListener
    {
        private readonly ILog _logger;
        private int _currentEpoch;
        private IResource _gtpFixture;
        private string _sport;


        public bool FixtureEnded { get; private set; }

        public StreamListener(IResource gtpFixture, int currentEpoch, string sport)
        {
            _logger = LogManager.GetLogger(typeof(StreamListener).ToString());
            FixtureEnded = false;
            _gtpFixture = gtpFixture;
            _sport = sport;
            _currentEpoch = currentEpoch;
            Listen();
        }

        private void Listen()
        {
            try
            {
                _gtpFixture.StreamConnected += GtpFixtureStreamConnected;
                _gtpFixture.StreamDisconnected += GtpFixtureStreamDisconnected;
                _gtpFixture.StreamEvent += GtpFixtureStreamEvent;
                _gtpFixture.StreamSynchronizationError += GtpFixtureStreamSynchronizationError;
                _gtpFixture.StartStreaming();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }
        private void GtpFixtureStreamDisconnected(object sender, EventArgs e)
        {
            _logger.InfoFormat("Stream disconnected for {0}", _gtpFixture.Name);
        }

        private void GtpFixtureStreamConnected(object sender, EventArgs e)
        {
            _logger.InfoFormat("Stream connected for {0}", _gtpFixture.Name);
        }

        private void GtpFixtureStreamSynchronizationError(object sender, EventArgs e)
        {
            _logger.WarnFormat("Stream out of sync for {0}",_gtpFixture.Name);
            var resource = sender as IResource;

            resource.PauseStreaming();
            var snapshotString = resource.GetSnapshot();
            var fixtureSnapshot =
                        (Fixture)
                        JsonConvert.DeserializeObject(snapshotString, typeof(Fixture),
                                                      new JsonSerializerSettings
                                                      {
                                                          Converters = new List<JsonConverter> { new IsoDateTimeConverter() },
                                                          NullValueHandling = NullValueHandling.Ignore
                                                      });
            _currentEpoch = fixtureSnapshot.Epoch;
            if (fixtureSnapshot.MatchStatus != "50")
            {
                resource.UnPauseStreaming();
            }
        }

        private void GtpFixtureStreamEvent(object sender, StreamEventArgs e)
        {
            try
            {
                var resource = sender as IResource;

                var streamMessage = (StreamMessage)JsonConvert.DeserializeObject(e.Update, typeof(StreamMessage),
                                                       new JsonSerializerSettings
                                                       {
                                                           Converters = new List<JsonConverter> { new IsoDateTimeConverter() },
                                                           NullValueHandling = NullValueHandling.Ignore
                                                       });

                var fixtureDelta = streamMessage.GetContent<Fixture>();
                _logger.InfoFormat("Attempting to process Markets and Selections for {0}", resource.Name);

                if (fixtureDelta.Epoch > _currentEpoch)
                {
                    _logger.InfoFormat("Epoch changed for {0} from {1} to {2}", _gtpFixture.Name, _currentEpoch, fixtureDelta.Epoch);
                    resource.PauseStreaming();

                    _logger.InfoFormat("Get UDAPI Snapshot for {0}", _gtpFixture.Name);
                    var snapshotString = _gtpFixture.GetSnapshot();
                    _logger.InfoFormat("Successfully retrieved UDAPI Snapshot for {0}", _gtpFixture.Name);

                    var fixtureSnapshot =
                        (Fixture)
                        JsonConvert.DeserializeObject(snapshotString, typeof(Fixture),
                                                      new JsonSerializerSettings
                                                      {
                                                          Converters = new List<JsonConverter> { new IsoDateTimeConverter() },
                                                          NullValueHandling = NullValueHandling.Ignore
                                                      });
                    _currentEpoch = fixtureSnapshot.Epoch;

                    //do something
                    _logger.Info(fixtureSnapshot);

                    if (fixtureDelta.MatchStatus != "50")
                    {
                        resource.UnPauseStreaming();
                    }
                    else
                    {
                        _logger.InfoFormat("Stopping Streaming for {0} with id {1}, Match Status is Match Over", _gtpFixture.Name, _gtpFixture.Id);
                        _gtpFixture.StopStreaming();
                        FixtureEnded = true;
                    }
                }
                else if (fixtureDelta.Epoch == _currentEpoch)
                {
                    //do something
                    _logger.Info(fixtureDelta.Markets.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }
    }
}
