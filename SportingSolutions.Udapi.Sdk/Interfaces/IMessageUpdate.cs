namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IMessageUpdate
    {
        string Id { get; set; }
        string Message { get; set; }
        bool IsEcho { get; set; }
    }
}
