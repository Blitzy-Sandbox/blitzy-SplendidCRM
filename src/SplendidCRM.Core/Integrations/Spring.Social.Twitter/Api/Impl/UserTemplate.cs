#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class UserTemplate : AbstractTwitterOperations, IUserOperations
    {
        public long GetProfileId() { return 0; }
        public string GetScreenName() { return string.Empty; }
        public TwitterProfile GetUserProfile() { return null; }
        public TwitterProfile GetUserProfile(string screenName) { return null; }
        public TwitterProfile GetUserProfile(long userId) { return null; }
        public List<TwitterProfile> GetUsers(params long[] userIds) { return new List<TwitterProfile>(); }
        public List<TwitterProfile> GetUsers(params string[] screenNames) { return new List<TwitterProfile>(); }
        public List<TwitterProfile> SearchForUsers(string query) { return new List<TwitterProfile>(); }
        public List<TwitterProfile> SearchForUsers(string query, int page, int pageSize) { return new List<TwitterProfile>(); }
        public List<SuggestionCategory> GetSuggestionCategories() { return new List<SuggestionCategory>(); }
        public List<TwitterProfile> GetSuggestions(string slug) { return new List<TwitterProfile>(); }
        public RateLimitStatus GetRateLimitStatus() { return null; }
    }
}
