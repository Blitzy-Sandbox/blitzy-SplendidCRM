#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    public interface IEventOperations { IList<Event> GetEvents(); Event GetEvent(string id); Event CreateEvent(Event ev); Event UpdateEvent(Event ev); void DeleteEvent(string id); }
}
