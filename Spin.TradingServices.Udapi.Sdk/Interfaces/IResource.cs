using System;
using Spin.TradingServices.Udapi.Sdk.Events;
using Spin.TradingServices.Udapi.Sdk.Model;

namespace Spin.TradingServices.Udapi.Sdk.Interfaces
{
    public interface IResource
    {
        string Id { get; }
        string Name { get; }
        Summary Content { get; }

        string GetSnapshot();
        void StartStreaming();
        void StopStreaming();

        event EventHandler StreamConnected;
        event EventHandler StreamDisconnected;
        event EventHandler<StreamEventArgs> StreamEvent;
    }
}
