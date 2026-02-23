#nullable disable
namespace Spring.Social.Office365.Api { public interface IFolderOperations { object GetFolders(); object GetFolder(string id); string CreateFolder(object folder); void DeleteFolder(string id); } }
