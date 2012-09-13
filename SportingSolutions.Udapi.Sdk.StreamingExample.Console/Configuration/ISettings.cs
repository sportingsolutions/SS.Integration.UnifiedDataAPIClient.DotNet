namespace SportingSolutions.Udapi.Sdk.StreamingExample.Console.Configuration
{
    public interface ISettings
    {
        string User { get; }
        string Password { get; }
        string Url { get; }
        int FixtureCheckerFrequency { get; }
        int StartingRetryDelay { get; }
        int MaxRetryDelay { get; }
        int MaxRetryAttempts { get; }
        int EchoInterval { get; }
        int EchoMaxDelay { get; }
    }
}