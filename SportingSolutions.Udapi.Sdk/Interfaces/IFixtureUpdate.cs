namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    internal interface IFixtureUpdate
    {
        string Id { get; set; }
        string Message { get; set; }
        bool IsEcho { get; set; }
    }
}