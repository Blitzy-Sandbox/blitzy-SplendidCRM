#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface ISearchOperations
    {
        SearchResults Search(string query);
        SearchResults Search(string query, int count);
        SearchResults Search(string query, int count, long sinceId, long maxId);
        List<SavedSearch> GetSavedSearches();
        SavedSearch GetSavedSearch(long searchId);
        SavedSearch CreateSavedSearch(string query);
        void DeleteSavedSearch(long searchId);
        Trends GetTrends(long whereOnEarthId);
        Trends GetTrends(long whereOnEarthId, bool excludeHashtags);
    }
}
