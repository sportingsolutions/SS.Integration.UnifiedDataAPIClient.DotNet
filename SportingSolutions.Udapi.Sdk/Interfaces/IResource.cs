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
        void PauseStreaming();
        void UnPauseStreaming();
        void StopStreaming();

        event EventHandler StreamConnected;
        event EventHandler StreamDisconnected;
        event EventHandler<StreamEventArgs> StreamEvent;
    }
}
