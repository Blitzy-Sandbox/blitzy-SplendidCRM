#nullable disable
namespace Spring.Social.Office365.Api { public interface IMailOperations { object GetMessages(string folderId); object GetMessage(string id); void SendMessage(object message); void DeleteMessage(string id); void MoveMessage(string id, string destinationId); } }
