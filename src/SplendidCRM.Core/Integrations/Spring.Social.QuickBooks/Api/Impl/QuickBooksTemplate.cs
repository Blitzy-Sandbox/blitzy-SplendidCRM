#nullable disable
using Spring.Social.QuickBooks.Api;
namespace Spring.Social.QuickBooks.Api.Impl { public class QuickBooksTemplate : IQuickBooks { public QuickBooksTemplate(string accessToken) { IsAuthorized = true; } public bool IsAuthorized { get; private set; } } }
