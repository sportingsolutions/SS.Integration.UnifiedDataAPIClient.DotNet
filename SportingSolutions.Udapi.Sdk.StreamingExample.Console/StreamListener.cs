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
using System.Linq;
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Configuration;
using SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model;
using log4net;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console
{
    public class StreamListener
    {
        private readonly ILog _logger;
        private readonly IResource _gtpFixture;
        private int _currentEpoch;
        private int _currentSequence;
        private readonly ISettings _settings;

        private string Id { get; set; }

        public bool FixtureEnded { get; private set; }

        public StreamListener(IResource gtpFixture, int currentEpoch)
        {
            _logger = LogManager.GetLogger(typeof(StreamListener));
            FixtureEnded = false;
            _gtpFixture = gtpFixture;
            _currentEpoch = currentEpoch;
            _settings = Settings.Instance;
            Id = _gtpFixture.Id;
            _currentSequence = -1;

            Listen();
        }

        public void StopListening()
        {
            if (_gtpFixture != null)
            {
                _gtpFixture.StreamConnected -= GtpFixtureStreamConnected;
                _gtpFixture.StreamDisconnected -= GtpFixtureStreamDisconnected;
                _gtpFixture.StreamEvent -= GtpFixtureStreamEvent;
                _gtpFixture.StopStreaming();
            }
        }

        private void Listen()
        {
            try
            {
                _gtpFixture.StreamConnected += GtpFixtureStreamConnected;
                _gtpFixture.StreamDisconnected += GtpFixtureStreamDisconnected;
                _gtpFixture.StreamEvent += GtpFixtureStreamEvent;

                _gtpFixture.StartStreaming(_settings.EchoInterval, _settings.EchoMaxDelay);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }
        private void GtpFixtureStreamDisconnected(object sender, EventArgs e)
        {
            if (!FixtureEnded)
            {
                _logger.WarnFormat("Stream disconnected due to problem with {0}, suspending markets, will try reconnect within 1 minute", _gtpFixture.Name);
                FixtureEnded = true;

                //The Stream has disconnected but the fixture hasn't ended, must be in an error state
                //Probably should suspend all markets for this fixture
                SuspendAllMarkets();
            }
            else
            {
                _logger.InfoFormat("Stream disconnected for {0}", _gtpFixture.Name);
            }
        }

        private void GtpFixtureStreamConnected(object sender, EventArgs e)
        {
            _logger.InfoFormat("Stream connected for {0}", _gtpFixture.Name);
        }


        private void GtpFixtureStreamEvent(object sender, StreamEventArgs e)
        {
            try
            {

                //Notice the two-step deserialization
                var streamMessage = e.Update.FromJson<StreamMessage>();
                var fixtureDelta = streamMessage.GetContent<Fixture>();

                _logger.InfoFormat("Streaming Update arrived for {0} id {1} sequence {2}", _gtpFixture.Name, _gtpFixture.Id, fixtureDelta.Sequence);

                if (fixtureDelta.Sequence < _currentSequence)
                {
                    _logger.InfoFormat("Fixture {0} id {1} sequence {2} is less than current sequence {3}", _gtpFixture.Name, _gtpFixture.Id, fixtureDelta.Sequence, _currentSequence);
                    return;
                }

                if ((fixtureDelta.Sequence - _currentSequence) > 1)
                {
                    _logger.WarnFormat("Fixture {0} id {1} sequence {2} is more than one greater that current sequence {3}", _gtpFixture.Name, _gtpFixture.Id, fixtureDelta.Sequence, _currentSequence);
                }

                _currentSequence = fixtureDelta.Sequence;

                //If the stream epoch does not equal the current epoch, 
                //then we need to pause streaming, process the snapshot, carry on streaming
                if (fixtureDelta.Epoch > _currentEpoch)
                {
                    _logger.InfoFormat("Epoch changed for {0} from {1} to {2}", _gtpFixture.Name, _currentEpoch, fixtureDelta.Epoch);
                    _currentEpoch = fixtureDelta.Epoch;

                    if (fixtureDelta.LastEpochChangeReason != null && fixtureDelta.LastEpochChangeReason.Contains((int)SSEpochChangeReason.StartTime))
                    {
                        _logger.InfoFormat("Fixture {0} has had its start time changed", _gtpFixture.Name);
                        ProcessDelta(fixtureDelta);
                    }

                    if (fixtureDelta.LastEpochChangeReason != null && fixtureDelta.LastEpochChangeReason.Contains((int)SSEpochChangeReason.Deleted))
                    {
                        _logger.InfoFormat("Fixture {0} has been deleted from the GTP Fixture Factroy. Suspending all markets and stopping the stream", _gtpFixture.Name);
                        _gtpFixture.StopStreaming();
                        SuspendAllMarkets();
                        FixtureEnded = true;
                    }
                    else
                    {
                        SuspendAndReprocessSnapshot();
                    }
                }
                else if (fixtureDelta.Epoch == _currentEpoch)
                {
                    ProcessDelta(fixtureDelta);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private void SuspendAndReprocessSnapshot()
        {
            Fixture fixtureSnapshot = null;

            try
            {
                SuspendAllMarkets();

                _logger.InfoFormat("Get UDAPI Snapshot for {0}", _gtpFixture.Name);
                var snapshotString = _gtpFixture.GetSnapshot();
                _logger.InfoFormat("Successfully retrieved UDAPI Snapshot for {0}", _gtpFixture.Name);

                fixtureSnapshot = snapshotString.FromJson<Fixture>();

                //Process the snapshot and send to client system                
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("There has been an error when trying to Suspend and Reprocess snapshot for {0} - {1}", _gtpFixture.Name, _gtpFixture.Id), ex);
            }
            //If an error occured this may be null. Nothing we can do but unpasuse the stream
            if (fixtureSnapshot != null && fixtureSnapshot.MatchStatus == ((int)SSMatchStatus.MatchOver).ToString())
            {
                _logger.InfoFormat("Stopping Streaming for {0} with id {1}, Match Status is Match Over", _gtpFixture.Name, _gtpFixture.Id);
                _gtpFixture.StopStreaming();
                FixtureEnded = true;
            }
        }

        private void ProcessDelta(Fixture fixtureDelta)
        {
            //process the update and send the data into client system
        }

        private void SuspendAllMarkets()
        {

        }
    }
}
