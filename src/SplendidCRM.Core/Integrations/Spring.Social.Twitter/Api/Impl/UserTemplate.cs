#nullable disable
// .NET 10 Migration: Task-based async methods added to satisfy the updated IUserOperations interface.
// All async methods return completed Task stubs (Task.FromResult) — this is a dormant integration
// stub that compiles but is not expected to execute.
// GetProfileId() and GetScreenName() retained as extra class members (not part of IUserOperations
// interface contract) for internal use within the stub implementation.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class UserTemplate : AbstractTwitterOperations, IUserOperations
    {
        // Extra helpers not part of IUserOperations interface
        public long GetProfileId() { return 0; }
        public string GetScreenName() { return string.Empty; }

        // =====================================================================
        // IUserOperations — Synchronous implementations
        // =====================================================================

        public TwitterProfile GetUserProfile() { return null; }
        public TwitterProfile GetUserProfile(string screenName) { return null; }
        public TwitterProfile GetUserProfile(long userId) { return null; }
        public IList<TwitterProfile> GetUsers(params long[] userIds) { return new List<TwitterProfile>(); }
        public IList<TwitterProfile> GetUsers(params string[] screenNames) { return new List<TwitterProfile>(); }
        public IList<TwitterProfile> SearchForUsers(string query) { return new List<TwitterProfile>(); }
        public IList<TwitterProfile> SearchForUsers(string query, int page, int pageSize) { return new List<TwitterProfile>(); }
        public IList<SuggestionCategory> GetSuggestionCategories() { return new List<SuggestionCategory>(); }
        public IList<TwitterProfile> GetSuggestions(string slug) { return new List<TwitterProfile>(); }
        public RateLimitStatus GetRateLimitStatus() { return null; }

        // =====================================================================
        // IUserOperations — Task-based async stubs (.NET 10)
        // =====================================================================

        public Task<TwitterProfile> GetUserProfileAsync() { return Task.FromResult<TwitterProfile>(null); }
        public Task<IList<TwitterProfile>> GetUsersAsync(params long[] userIds) { return Task.FromResult<IList<TwitterProfile>>(new List<TwitterProfile>()); }
        public Task<IList<TwitterProfile>> SearchForUsersAsync(string query) { return Task.FromResult<IList<TwitterProfile>>(new List<TwitterProfile>()); }
        public Task<IList<SuggestionCategory>> GetSuggestionCategoriesAsync() { return Task.FromResult<IList<SuggestionCategory>>(new List<SuggestionCategory>()); }
        public Task<IList<TwitterProfile>> GetSuggestionsAsync(string slug) { return Task.FromResult<IList<TwitterProfile>>(new List<TwitterProfile>()); }
        public Task<RateLimitStatus> GetRateLimitStatusAsync() { return Task.FromResult<RateLimitStatus>(null); }
    }
}
