#nullable disable
namespace Spring.Social.LinkedIn.Api
{
    public interface ILinkedIn
    {
        bool IsAuthorized { get; }
        ICommunicationOperations CommunicationOperations { get; }
        IConnectionOperations ConnectionOperations { get; }
        INetworkUpdateOperations NetworkUpdateOperations { get; }
        IProfileOperations ProfileOperations { get; }
    }
}
