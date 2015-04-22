using System;

namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IStreamStatistics
    {
        string Id { get; }

        string Name { get; }

        DateTime LastMessageReceived { get; }

        DateTime LastStreamDisconnect { get; }

        bool IsStreamActive { get; }

        double EchoRoundTripInMilliseconds { get; }
    }
}