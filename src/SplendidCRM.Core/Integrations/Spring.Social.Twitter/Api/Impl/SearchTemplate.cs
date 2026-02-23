#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class SearchTemplate : AbstractTwitterOperations, ISearchOperations
    {
        public SearchResults Search(string query) { return null; }
        public SearchResults Search(string query, int count) { return null; }
        public SearchResults Search(string query, int count, long sinceId, long maxId) { return null; }
        public List<SavedSearch> GetSavedSearches() { return new List<SavedSearch>(); }
        public SavedSearch GetSavedSearch(long searchId) { return null; }
        public SavedSearch CreateSavedSearch(string query) { return null; }
        public void DeleteSavedSearch(long searchId) { }
        public Trends GetTrends(long whereOnEarthId) { return null; }
        public Trends GetTrends(long whereOnEarthId, bool excludeHashtags) { return null; }
    }
}
