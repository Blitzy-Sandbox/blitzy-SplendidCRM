#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface IBlockOperations
    {
        TwitterProfile Block(string screenName);
        TwitterProfile Block(long userId);
        TwitterProfile Unblock(string screenName);
        TwitterProfile Unblock(long userId);
        CursoredList<TwitterProfile> GetBlockedUsers();
        CursoredList<TwitterProfile> GetBlockedUsers(long cursor);
        CursoredList<long> GetBlockedUserIds();
        CursoredList<long> GetBlockedUserIds(long cursor);
    }
}
