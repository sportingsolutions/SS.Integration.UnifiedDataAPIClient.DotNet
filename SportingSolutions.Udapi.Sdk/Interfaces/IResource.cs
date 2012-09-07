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
using SportingSolutions.Udapi.Sdk.Events;
using SportingSolutions.Udapi.Sdk.Model;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IResource
    {
        string Id { get; }
        string Name { get; }
        Summary Content { get; }

        string GetSnapshot();
        void StartStreaming();
        void StartStreaming(int echoInterval, int echoMaxDelay);
        void PauseStreaming();
        void UnPauseStreaming();
        void StopStreaming();

        event EventHandler StreamConnected;
        event EventHandler StreamDisconnected;
        event EventHandler<StreamEventArgs> StreamEvent;
        event EventHandler StreamSynchronizationError;
    }
}
