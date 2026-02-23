#nullable disable
using Spring.Social.Office365.Api;
namespace Spring.Social.Office365.Api.Impl
{
    public class Office365Template : IOffice365
    {
        public Office365Template(string accessToken) { IsAuthorized = true; }
        public bool IsAuthorized { get; private set; }
        public IContactOperations ContactOperations { get; private set; }
        public IMailOperations MailOperations { get; private set; }
        public ICalendarOperations CalendarOperations { get; private set; }
        public IFolderOperations FolderOperations { get; private set; }
        public ISubscriptionOperations SubscriptionOperations { get; private set; }
    }
}
