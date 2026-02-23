#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface IUserOperations
    {
        long GetProfileId();
        string GetScreenName();
        TwitterProfile GetUserProfile();
        TwitterProfile GetUserProfile(string screenName);
        TwitterProfile GetUserProfile(long userId);
        List<TwitterProfile> GetUsers(params long[] userIds);
        List<TwitterProfile> GetUsers(params string[] screenNames);
        List<TwitterProfile> SearchForUsers(string query);
        List<TwitterProfile> SearchForUsers(string query, int page, int pageSize);
        List<SuggestionCategory> GetSuggestionCategories();
        List<TwitterProfile> GetSuggestions(string slug);
        RateLimitStatus GetRateLimitStatus();
    }
}
