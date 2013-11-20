namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    internal interface IMessageUpdate
    {
        string Id { get; set; }
        string Message { get; set; }
        bool IsEcho { get; set; }
    }
}
