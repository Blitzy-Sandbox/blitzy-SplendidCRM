#nullable disable
using Spring.Social.HubSpot.Api;
namespace Spring.Social.HubSpot.Api.Impl { public class HubSpotTemplate : IHubSpot { public HubSpotTemplate(string accessToken) { IsAuthorized = true; } public bool IsAuthorized { get; private set; } } }
