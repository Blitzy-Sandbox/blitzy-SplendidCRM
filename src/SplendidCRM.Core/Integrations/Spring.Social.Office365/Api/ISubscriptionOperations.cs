#nullable disable
namespace Spring.Social.Office365.Api { public interface ISubscriptionOperations { object GetSubscriptions(); string CreateSubscription(object sub); void DeleteSubscription(string id); void RenewSubscription(string id); } }
