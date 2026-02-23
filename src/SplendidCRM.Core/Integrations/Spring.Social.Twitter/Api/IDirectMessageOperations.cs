#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface IDirectMessageOperations
    {
        List<DirectMessage> GetDirectMessagesReceived();
        List<DirectMessage> GetDirectMessagesReceived(int count);
        List<DirectMessage> GetDirectMessagesReceived(int count, long sinceId, long maxId);
        List<DirectMessage> GetDirectMessagesSent();
        List<DirectMessage> GetDirectMessagesSent(int count);
        List<DirectMessage> GetDirectMessagesSent(int count, long sinceId, long maxId);
        DirectMessage GetDirectMessage(long id);
        DirectMessage SendDirectMessage(string toScreenName, string text);
        DirectMessage SendDirectMessage(long toUserId, string text);
        void DeleteDirectMessage(long id);
    }
}
