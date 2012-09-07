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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private readonly ILog _streamLogger;
        private readonly ILog _logger;
        private int _currentEpoch;
        private int _currentSequence = -1;
        private readonly IResource _gtpFixture;
        private string _sport;
        private List<Tuple<string, string, Dictionary<string, string>>> _names;

        private string Id { get; set; }

        public bool FixtureEnded { get; private set; }

        public StreamListener(IResource gtpFixture, int currentEpoch, string sport, ILog logger, List<Tuple<string, string, Dictionary<string, string>>> names)
        {
            _logger = LogManager.GetLogger(typeof(StreamListener).ToString());
            _names = names;
            _streamLogger = logger;
            FixtureEnded = false;
            _gtpFixture = gtpFixture;
            _sport = sport;
            _currentEpoch = currentEpoch;
            Id = _gtpFixture.Id;
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
            _logger.InfoFormat("Stream disconnected for {0} id {1}", _gtpFixture.Name, _gtpFixture.Id);
        }

        private void GtpFixtureStreamConnected(object sender, EventArgs e)
        {
            _logger.InfoFormat("Stream connected for {0} id {1}", _gtpFixture.Name, _gtpFixture.Id);
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


                var size = ASCIIEncoding.UTF8.GetByteCount(e.Update);
                double newsize = (double)size/(double)1024;

                var streamMessage = (StreamMessage)JsonConvert.DeserializeObject(e.Update, typeof(StreamMessage),
                                                       new JsonSerializerSettings
                                                       {
                                                           Converters = new List<JsonConverter> { new IsoDateTimeConverter() },
                                                           NullValueHandling = NullValueHandling.Ignore
                                                       });

                var fixtureDelta = streamMessage.GetContent<Fixture>();
                _streamLogger.InfoFormat("############################################################# \n");

                if (fixtureDelta.Epoch > _currentEpoch)
                {
                    var sb = new StringBuilder();
                    sb.Append(string.Format("sequence {0};size {1}kb;epoch {2};", fixtureDelta.Sequence, newsize, fixtureDelta.Epoch));
                    foreach (var epochChangeReason in fixtureDelta.LastEpochChangeReason)
                    {
                        sb.Append(GetEpochChangeReason(epochChangeReason));
                        sb.Append(";");
                    }
                    _streamLogger.InfoFormat(sb.ToString());
                }
                else
                {
                    _streamLogger.InfoFormat("sequence {0};size {1}kb", fixtureDelta.Sequence, newsize);    
                }
                

                if(fixtureDelta.Sequence < _currentSequence)
                {
                    _logger.InfoFormat("Fixture {0} id {1} sequence {2} is less than current sequence {3}", _gtpFixture.Name, _gtpFixture.Id, fixtureDelta.Sequence, _currentSequence);
                    return;   
                }
                if ((fixtureDelta.Sequence - _currentSequence) > 1)
                {
                    _logger.WarnFormat("Fixture {0} id {1} sequence {2} is more than one greater that current sequence {3}", _gtpFixture.Name, _gtpFixture.Id, fixtureDelta.Sequence, _currentSequence);
                }

                _currentSequence = fixtureDelta.Sequence;

                if (fixtureDelta.Epoch > _currentEpoch)
                {
                    _logger.InfoFormat("Epoch changed for {0} id {1} from {2} to {3}", _gtpFixture.Name, _gtpFixture.Id, _currentEpoch, fixtureDelta.Epoch);
                    if (fixtureDelta.LastEpochChangeReason != null && fixtureDelta.LastEpochChangeReason.Contains((int)SSEpochChangeReason.Deleted))
                    {
                        _logger.InfoFormat("Fixture {0} has been deleted from the GTP Fixture Factroy. Suspending all markets and stopping the stream", _gtpFixture.Name);
                        _gtpFixture.StopStreaming();
                        FixtureEnded = true;
                    }
                    else
                    {
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

                        _names = ProcessSnapshot(fixtureSnapshot);

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
                }
                else if (fixtureDelta.Epoch == _currentEpoch)
                {
                    //do something

                    ProcessDelta(fixtureDelta);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private void ProcessDelta(Fixture fixtureDelta)
        {
            try
            {
                Parallel.For(0, fixtureDelta.Markets.Count, new ParallelOptions {MaxDegreeOfParallelism = 10}, i =>
                    {
                        var market = fixtureDelta.Markets[i];
                        var marketName = market.Id;

                        Tuple<string, string, Dictionary<string, string>> t = null;

                        if(_names.Exists(x => x.Item1 == market.Id))
                        {
                            t = _names.Single(x => x.Item1 == market.Id);
                            marketName = t.Item2;
                        }

                        var message = new StringBuilder();
                        message.Append(string.Format("{0};{1};", fixtureDelta.Id, marketName));

                        foreach (var selection in market.Selections)
                        {
                            var selectionName = selection.Id;
                            if(t != null)
                            {
                                if(t.Item3.ContainsKey(selection.Id))
                                {
                                    selectionName = t.Item3[selection.Id];
                                }
                            }
                            message.Append(string.Format("{0};", selectionName));
                        }
                        _streamLogger.Info(message.ToString());
                    });
            }
            catch (Exception ex)
            {
                _logger.Error("ProcessDelta",ex);
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
                if (market.Tags.ContainsKey("name"))
                {
                    marketName = market.Tags["name"].ToString();
                }
                var selections = new Dictionary<string, string>();
                foreach (var selection in market.Selections)
                {
                    var selectionId = selection.Id;
                    var selectionName = selection.Id;
                    if (selection.Tags.ContainsKey("name"))
                    {
                        selectionName = selection.Tags["name"].ToString();
                    }
                    selections.Add(selectionId, selectionName);
                }
                t = new Tuple<string, string, Dictionary<string, string>>(marketId, marketName, selections);
                name.Add(t);
            }
            return name;
        }

        private string GetEpochChangeReason(int ecr)
        {
            var result = "";
            switch(ecr)
            {
                case 0:
                    result = "Created";
                    break;
                case 5:
                    result = "Unpublished";
                    break;
                case 10:
                    result = "Deleted";
                    break;
                case 20:
                    result = "Participants";
                    break;
                case 30:
                    result = "StartTime";
                    break;
                case 40:
                    result = "MatchStatus";
                    break;
                case 50:
                    result = "BaseVariables";
                    break;
                case 60:
                    result = "Definition";
                    break;
                default:
                    result = "Unknown";
                    break;
            }
            return result;
        }
    }
}
