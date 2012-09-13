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
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Model
{
    public class Fixture
    {
        public Fixture()
        {
            Tags = new Dictionary<string, object>();
            GameState = new Dictionary<string, object>();
            Markets = new List<Market>();
            Participants = new List<Participant>();
        }

        public int Epoch { get; set; }

        public int[] LastEpochChangeReason { get; set; }

        public string Id { get; set; }

        public DateTime? StartTime { get; set; }

        public int Sequence { get; set; }

        public string MatchStatus { get; set; }

        public Dictionary<string, object> Tags { get; set; }

        public Dictionary<string, object> GameState { get; set; }

        public List<Market> Markets { get; set; }

        public List<Participant> Participants { get; set; } 
    }
}
