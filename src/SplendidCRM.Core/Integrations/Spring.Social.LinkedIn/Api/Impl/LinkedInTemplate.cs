#nullable disable
using Spring.Social.LinkedIn.Api;
namespace Spring.Social.LinkedIn.Api.Impl
{
    public class LinkedInTemplate : ILinkedIn
    {
        public LinkedInTemplate(string accessToken) { IsAuthorized = true; }
        public bool IsAuthorized { get; private set; }
        public ICommunicationOperations CommunicationOperations { get; private set; }
        public IConnectionOperations ConnectionOperations { get; private set; }
        public INetworkUpdateOperations NetworkUpdateOperations { get; private set; }
        public IProfileOperations ProfileOperations { get; private set; }
    }
}
