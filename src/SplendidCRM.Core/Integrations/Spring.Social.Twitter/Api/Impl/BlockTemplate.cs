#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class BlockTemplate : AbstractTwitterOperations, IBlockOperations
    {
        public TwitterProfile Block(string screenName) { return null; }
        public TwitterProfile Block(long userId) { return null; }
        public TwitterProfile Unblock(string screenName) { return null; }
        public TwitterProfile Unblock(long userId) { return null; }
        public CursoredList<TwitterProfile> GetBlockedUsers() { return new CursoredList<TwitterProfile>(); }
        public CursoredList<TwitterProfile> GetBlockedUsers(long cursor) { return new CursoredList<TwitterProfile>(); }
        public CursoredList<long> GetBlockedUserIds() { return new CursoredList<long>(); }
        public CursoredList<long> GetBlockedUserIds(long cursor) { return new CursoredList<long>(); }
    }
}
