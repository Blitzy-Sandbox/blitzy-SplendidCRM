#nullable disable
// .NET 10 Migration: Added RestOperations property to satisfy the updated ILinkedIn interface
// contract after IApiBinding and IRestOperations stubs were inlined into the namespace
// (per AAP Section 0.7.4 — Spring.Social Dependency Removal).
// This is a dormant Enterprise Edition integration stub — compile only, not activated.
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
        public IRestOperations RestOperations { get; private set; }
    }
}
