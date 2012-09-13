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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using RestSharp;
using SportingSolutions.Udapi.Sdk.Clients;
using SportingSolutions.Udapi.Sdk.Extensions;
using SportingSolutions.Udapi.Sdk.Model;
using System;

namespace SportingSolutions.Udapi.Sdk.Tests
{
    [TestFixture]
    internal class ResourceTests
    {
        [SetUp]
        public void InitialiseStreamController()
        {
            ((Configuration)UDAPI.Configuration).UseEchos = false;

            // register a mocked version of StreamController that doesn't 
            // require any AMPQ server. It behaves exactly as StreamController,
            // except for the fact that doesn't establish a connection
            MockedStreamController.Register(new UpdateDispatcher());
        }

        /// <summary>
        ///     Creates a RestItem object for testing purposes as
        ///     it was returned by the UDAPI service
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        private static RestItem GetRestItem(Dictionary<string, string> properties)
        {
            string id = properties.ContainsKey("id") ? properties["id"] : "UnknownResourceId";
            string sport = properties.ContainsKey("sport") ? properties["sport"] : "Football";


            // RestItem as returned by the UDAPI service
            RestItem item = new RestItem {
                Name = properties.ContainsKey("name") ? properties["name"] : "UnknownResource",
                Content = new Summary {
                    Id = id,
                    MatchStatus = properties.ContainsKey("matchstatus") ? Convert.ToInt32(properties["matchstatus"]) : 40,
                    Sequence = properties.ContainsKey("sequence") ? Convert.ToInt32(properties["sequence"]) : 1,
                    StartTime = properties.ContainsKey("starttime") ? properties["starttime"] : DateTime.UtcNow.ToString(),
                    Tags = new List<Tag>
                    {
                        new Tag
                        {
                            Id = 2,
                            Key = "Competition",
                            Value = properties.ContainsKey("competition") ? properties["competition"] : "UnknownCompetition"
                        },
                        new Tag
                        {
                            Id = 1,
                            Key = "Participant",
                            Value = properties.ContainsKey("participant2") ? properties["participant2"] : "UnknownAwayParticipant"
                        },
                        new Tag
                        {
                            Id = 0,
                            Key = "Participant",
                            Value = properties.ContainsKey("participant1") ? properties["participant1"] : "UnknownHomeParticipant"
                        }
                    }
                },

                Links = new List<RestLink>(),
            };

            // links to the resource services
            var restlink = new RestLink {
                Relation = "http://api.sportingsolutions.com/rels/snapshot",
                Href = string.Format("http://apicui.sportingsolutions.com/UnifiedDataAPI/snapshot/{0}/{1}/fake-random-token-0", sport, id),
                Verbs = new[] { "GET" }
            };

            item.Links.Add(restlink);

            restlink = new RestLink {
                Relation = "http://api.sportingsolutions.com/rels/stream/amqp",
                Href = string.Format("http://apicui.sportingsolutions.com/UnifiedDataAPI/strean/{0}/{1}/fake-random-token-1", sport, id),
                Verbs = new[] { "GET" }
            };
            item.Links.Add(restlink);

            restlink = new RestLink {
                Relation = "http://api.sportingsolutions.com/rels/sequence",
                Href = string.Format("http://apicui.sportingsolutions.com/UnifiedDataAPI/sequence/{0}/{1}/fake-random-token-2", sport, id),
                Verbs = new[] { "GET" }
            };
            item.Links.Add(restlink);

            restlink = new RestLink {
                Relation = "http://api.sportingsolutions.com/rels/stream/echo",
                Href = "http://apicui.sportingsolutions.com/EchoRestService/echo/fake-random-token-3",
                Verbs = new[] { "POST" }
            };
            item.Links.Add(restlink);

            restlink = new RestLink {
                Relation = "http://api.sportingsolutions.com/rels/stream/batchecho",
                Href = "http://apicui.sportingsolutions.com/EchoRestService/batchecho/fake-random-token-4",
                Verbs = new[] { "POST" }
            };
            item.Links.Add(restlink);

            return item;
        }

        /// <summary>
        ///     Utility method to set up a mocked Resource object.
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        private static Resource GetResourceForTest(Dictionary<string, string> properties)
        {
            RestItem item = GetRestItem(properties);

            // fake response for getting AMPQ details
            var streamLink = item.Links.FirstOrDefault(x => string.Equals(x.Relation, "http://api.sportingsolutions.com/rels/stream/amqp"));

            var streamResponse = new RestResponse<List<RestItem>> {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = "[{\"Name\":\"stream\",\"Links\":[{\"Relation\":\"amqp\",\"Href\":\"amqp://test%40testco:password@localhost/testco/amq.gen-nntsI7-nQIJSpqEOV1N98w\"}]}]"
            };

            streamResponse.Data = streamResponse.Content.FromJson<List<RestItem>>();

            // mock the request of getting AMPQ details
            var client = new Mock<IConnectClient>();
            client.Setup(x => x.Request<List<RestItem>>(
                It.Is<Uri>(y => string.Equals(y.ToString(), streamLink.Href)),
                It.Is<Method>(y => y == Method.GET))
                ).Returns(streamResponse);
              
            
            return new Resource(item, client.Object);
        }

        /// <summary>
        ///     Here I want to test that Resource.StartStreaming()
        ///     and Resource.StopStreaming() correctly register 
        ///     consumers on the streaming queue and raise the 
        ///     appropriate events.
        /// </summary>
        [Test]
        public void StartStopStreamingTest()
        {
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED, "Pre-requirements fail");

            // STEP 1: prepare the mocked data
            Dictionary<string, string> properties =  new Dictionary<string, string>
            {
                {"id", "ResourceTest1"},
                {"matchstatus", "40"}
            };


            bool connectedRaised = false;
            bool disconnectedRaised = false;
            object _lock = new object();

            Resource resource = GetResourceForTest(properties);

            // STEP 2: register the "connected" and "disconnected" event handlers
            resource.StreamConnected += (sender, ea) => { connectedRaised = true; lock (_lock) { Monitor.PulseAll(_lock); } };
            resource.StreamDisconnected += (sender, ea) => { disconnectedRaised = true; lock (_lock) { Monitor.PulseAll(_lock); } };


            // STEP 3: start streaming
            resource.StartStreaming();

            // events are raised in an async way...so wait a bit
            lock (_lock)
            {
                Monitor.Wait(_lock, 2000);
            }

            
            connectedRaised.Should().BeTrue("Connection event wasn't raised");
            disconnectedRaised.Should().BeFalse("Disconnection event wasn't raised");
            StreamController.Instance.Dispatcher.HasSubscriber(resource.Id).Should().BeTrue("Consumer wasn't correctly registred");

            // STEP 4: stop streaming
            resource.StopStreaming();

            lock(_lock)
            {
                Monitor.Wait(_lock, 2000);
            }

            disconnectedRaised.Should().BeTrue("Disconnection event wasn't raised");
            StreamController.Instance.Dispatcher.HasSubscriber(resource.Id).Should().BeFalse("Consumer wasn't correctly registred");

            // once the connection is open, we don't close it unless Dispose() is called
            StreamController.Instance.State.Should().Be(StreamController.ConnectionState.CONNECTED);
        }
    
        /// <summary>
        ///     Similar to StartStopStreamingTest() but with a large
        ///     number of resources.
        /// </summary>
        [Test]
        public void StartStopStreamingOnMultipleResourcesTest()
        {
            
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED, "Pre-requirements fail");

            // STEP 1: prepare the test data
            Resource[] resources = new Resource[10000];
            int[] connections = new int[10000];
            int[] disconnections = new int[10000];

            for (int i = 0; i < 10000; i++)
            {
                // these will track the disconnection and connection events
                connections[i] = 0;
                disconnections[i] = 0;

                Dictionary<string, string> properties = new Dictionary<string, string>
                {
                    {"id", "ResourceTest_" + i },
                    {"matchstatus", "40"}
                };

                Resource resource = GetResourceForTest(properties);
                resources[i] = resource;

                resource.StreamConnected += (sender, e) => 
                    {
                        Assert.IsTrue(sender is Resource);
                        connections[Convert.ToInt32(((Resource)sender).Id.Substring(13))]++;
                    };

                resource.StreamDisconnected += (sender, e) => 
                    {
                        Assert.IsTrue(sender is Resource);
                        disconnections[Convert.ToInt32(((Resource)sender).Id.Substring(13))]++;    
                    };

                resource.StartStreaming();
            }

         
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED, "Connection wasn't established");

            // STEP 2: check that all consumers are correctly registred witht the Dispatcher
            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(10000, "Not all consumers were correctly registred");

            foreach(var r in resources)
                StreamController.Instance.Dispatcher.HasSubscriber(r.Id).Should().BeTrue("Consumer " + r.Id + " wasn't correctly registred");
            

            // STEP 3: StopStreaming() for all resources
            for(int i = 0; i < 10000; i++)
            {
                resources[i].StopStreaming();
            }


            // give a little bit of time to process the messages
            Thread.Sleep(2000);
            Thread.Yield();

            // STEP 4: check the results
            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(0, "Not all consumer were correctly un-registred");

            for(int i = 0; i < 10000; i++)
            {
                connections[i].Should().Be(1, "Connection event wasn't correctly raised");
                disconnections[i].Should().Be(1, "Disconnection event wasn't correctly raised");
            }
        }

        /// <summary>
        ///     In this test I want to make sure that a Resource object (identified by
        ///     its id) is only registred once on the streaming queue
        /// </summary>
        [Test]
        public void NoDuplicatesResourceTest()
        {
            
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED, "Pre-requirements fail");

            // STEP 1: Preapare mocked data
            Dictionary<string, string> properties = new Dictionary<string, string>
            {
                {"id", "ResourceTest1"},
                {"matchstatus", "40"}
            };


            Resource resource = GetResourceForTest(properties);
          
            // STEP 2: call StartStreaming() on the resource object
            resource.StartStreaming();

            // events are raised in an async way...so wait a bit
            Thread.Sleep(1000);
            Thread.Yield();

            // STEP 3: check that the consumer is correctly registred
            StreamController.Instance.Dispatcher.HasSubscriber(resource.Id).Should().BeTrue("Consumer wansn't correctly registred");
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED, "Connection wasn't established");


            // STEP 4: try to register the consumer a second time
            bool ok = false;

            try
            {
                // this should raise an exception
                resource.StartStreaming();
            }
            catch
            {
                ok = true;
            }

            
            ok.Should().BeTrue("StreamController allowed a consumer to be registred twice");
            StreamController.Instance.Dispatcher.HasSubscriber(resource.Id).Should().BeTrue("Consumer was erroneously removed");
        }
    
        /// <summary>
        ///     In this test I want to make sure
        ///     that the dispatcher correctly dispatches
        ///     messages to the resource objects
        /// </summary>
        [Test]
        public void ConsumeMessagesTest()
        {
            
            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.DISCONNECTED, "Pre-requirements fail");

            // STEP 1: prepare the test data
            Resource[] resources = new Resource[10000];
            int[] messages = new int[10000];

            for (int i = 0; i < 10000; i++)
            {
                messages[i] = 0;

                Dictionary<string, string> properties = new Dictionary<string, string>
                {
                    {"id", "ResourceTest_" + i },
                    {"matchstatus", "40"}
                };

                Resource resource = GetResourceForTest(properties);
                resources[i] = resource;

                resource.StreamEvent += (sender, ea) => 
                    {
                        messages[Convert.ToInt32(((Resource)sender).Id.Substring(13))]++;
                    };

                resource.StartStreaming();
            }

            StreamController.Instance.State.ShouldBeEquivalentTo(StreamController.ConnectionState.CONNECTED, "Connection was not established");

            // STEP 2: check that all consumers are correctly registred witht the Dispatcher
            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(10000, "Not all consumer were correctly registred");

            foreach (var r in resources)
                StreamController.Instance.Dispatcher.HasSubscriber(r.Id).Should().BeTrue("Consumer " + r.Id + " wasn't correctly registred");


            // STEP 3: Send 3 updates to the first 5000 resources
            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < 5000; i++)
                {
                    StreamController.Instance.Dispatcher.DispatchMessage("ResourceTest_" + i, "UPDATE_" + j).Should().BeTrue();
                }
            }

            // give a little bit of time to process the messages
            Thread.Sleep(3000);
            Thread.Yield();

            // STEP 4: check the results
            StreamController.Instance.Dispatcher.SubscribersCount.Should().Be(10000, "Some consumers were erroneously removed");

            for (int i = 0; i < 10000; i++)
            {
                if(i < 5000)
                {
                    messages[i].Should().Be(3, "Not all updates arrived for consumer " + i);
                }
                else
                {
                    messages[i].Should().Be(0, "Updates were not supposed to arrive for consumer " + i);
                }
            }
        }
 
    }
}
