#nullable disable
namespace Spring.Social.Office365.Api { public interface ICalendarOperations { object GetEvents(); object GetEvent(string id); string CreateEvent(object evt); void UpdateEvent(string id, object evt); void DeleteEvent(string id); } }
