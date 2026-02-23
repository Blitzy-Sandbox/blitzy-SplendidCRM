#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class FeedTemplate : AbstractFacebookOperations, IFeedOperations
    {
        public FeedTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Post> GetFeed() { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetFeed(int offset, int limit) { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetFeed(string ownerId) { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetFeed(string ownerId, int offset, int limit) { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetHomeFeed() { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetHomeFeed(int offset, int limit) { requireAuthorization(); return new List<Post>(); }
        public Post GetPost(string entryId) { requireAuthorization(); return default(Post); }
        public List<StatusPost> GetStatuses() { requireAuthorization(); return new List<StatusPost>(); }
        public List<StatusPost> GetStatuses(int offset, int limit) { requireAuthorization(); return new List<StatusPost>(); }
        public List<StatusPost> GetStatuses(string userId) { requireAuthorization(); return new List<StatusPost>(); }
        public List<StatusPost> GetStatuses(string userId, int offset, int limit) { requireAuthorization(); return new List<StatusPost>(); }
        public List<LinkPost> GetLinks() { requireAuthorization(); return new List<LinkPost>(); }
        public List<LinkPost> GetLinks(int offset, int limit) { requireAuthorization(); return new List<LinkPost>(); }
        public List<LinkPost> GetLinks(string ownerId) { requireAuthorization(); return new List<LinkPost>(); }
        public List<LinkPost> GetLinks(string ownerId, int offset, int limit) { requireAuthorization(); return new List<LinkPost>(); }
        public List<NotePost> GetNotes() { requireAuthorization(); return new List<NotePost>(); }
        public List<NotePost> GetNotes(int offset, int limit) { requireAuthorization(); return new List<NotePost>(); }
        public List<NotePost> GetNotes(string ownerId) { requireAuthorization(); return new List<NotePost>(); }
        public List<NotePost> GetNotes(string ownerId, int offset, int limit) { requireAuthorization(); return new List<NotePost>(); }
        public List<Post> GetPosts() { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetPosts(int offset, int limit) { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetPosts(string ownerId) { requireAuthorization(); return new List<Post>(); }
        public List<Post> GetPosts(string ownerId, int offset, int limit) { requireAuthorization(); return new List<Post>(); }
        public string UpdateStatus(string message) { requireAuthorization(); var nvc = new NameValueCollection(); nvc.Add("message", message); return Publish("me", "feed", nvc); }
        public string PostLink(string message, FacebookLink link) { requireAuthorization(); return string.Empty; }
        public string Post(string ownerId, string message) { requireAuthorization(); return string.Empty; }
        public string PostLink(string ownerId, string message, FacebookLink link) { requireAuthorization(); return string.Empty; }
        public void DeletePost(string id) { requireAuthorization(); Delete(id); }
        public List<Post> SearchPublicFeed(string query) { return SearchPublicFeed(query, 0, 25); }
        public List<Post> SearchPublicFeed(string query, int offset, int limit) { return new List<Post>(); }
        public List<Post> SearchHomeFeed(string query) { return SearchHomeFeed(query, 0, 25); }
        public List<Post> SearchHomeFeed(string query, int offset, int limit) { requireAuthorization(); return new List<Post>(); }
        public List<Post> SearchUserFeed(string query) { return SearchUserFeed("me", query, 0, 25); }
        public List<Post> SearchUserFeed(string query, int offset, int limit) { return SearchUserFeed("me", query, offset, limit); }
        public List<Post> SearchUserFeed(string userId, string query) { return SearchUserFeed(userId, query, 0, 25); }
        public List<Post> SearchUserFeed(string userId, string query, int offset, int limit) { requireAuthorization(); return new List<Post>(); }
    }
}
