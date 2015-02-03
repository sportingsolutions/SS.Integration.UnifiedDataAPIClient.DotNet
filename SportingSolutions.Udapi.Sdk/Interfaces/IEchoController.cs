namespace SportingSolutions.Udapi.Sdk.Interfaces
{
    public interface IEchoController
    {
        void StartEchos(string virtualHost, int echoInterval);
        void StopEchos();
    }
}
