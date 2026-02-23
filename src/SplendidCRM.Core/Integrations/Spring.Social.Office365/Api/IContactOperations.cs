#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Office365.Api { public interface IContactOperations { object GetContacts(); object GetContact(string id); string CreateContact(object contact); void UpdateContact(string id, object contact); void DeleteContact(string id); } }
