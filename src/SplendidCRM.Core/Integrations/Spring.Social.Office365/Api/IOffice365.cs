#nullable disable
namespace Spring.Social.Office365.Api
{
    public interface IOffice365
    {
        bool IsAuthorized { get; }
        IContactOperations ContactOperations { get; }
        IMailOperations MailOperations { get; }
        ICalendarOperations CalendarOperations { get; }
        IFolderOperations FolderOperations { get; }
        ISubscriptionOperations SubscriptionOperations { get; }
    }
}
