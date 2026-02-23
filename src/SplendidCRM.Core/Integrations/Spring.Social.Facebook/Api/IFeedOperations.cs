#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IFeedOperations
    {
        List<Post> GetFeed();
        List<Post> GetFeed(int offset, int limit);
        List<Post> GetFeed(string ownerId);
        List<Post> GetFeed(string ownerId, int offset, int limit);
        List<Post> GetHomeFeed();
        List<Post> GetHomeFeed(int offset, int limit);
        Post GetPost(string entryId);
        List<StatusPost> GetStatuses();
        List<StatusPost> GetStatuses(int offset, int limit);
        List<StatusPost> GetStatuses(string userId);
        List<StatusPost> GetStatuses(string userId, int offset, int limit);
        List<LinkPost> GetLinks();
        List<LinkPost> GetLinks(int offset, int limit);
        List<LinkPost> GetLinks(string ownerId);
        List<LinkPost> GetLinks(string ownerId, int offset, int limit);
        List<NotePost> GetNotes();
        List<NotePost> GetNotes(int offset, int limit);
        List<NotePost> GetNotes(string ownerId);
        List<NotePost> GetNotes(string ownerId, int offset, int limit);
        List<Post> GetPosts();
        List<Post> GetPosts(int offset, int limit);
        List<Post> GetPosts(string ownerId);
        List<Post> GetPosts(string ownerId, int offset, int limit);
        string UpdateStatus(string message);
        string PostLink(string message, FacebookLink link);
        string Post(string ownerId, string message);
        string PostLink(string ownerId, string message, FacebookLink link);
        void DeletePost(string id);
        List<Post> SearchPublicFeed(string query);
        List<Post> SearchPublicFeed(string query, int offset, int limit);
        List<Post> SearchHomeFeed(string query);
        List<Post> SearchHomeFeed(string query, int offset, int limit);
        List<Post> SearchUserFeed(string query);
        List<Post> SearchUserFeed(string query, int offset, int limit);
        List<Post> SearchUserFeed(string userId, string query);
        List<Post> SearchUserFeed(string userId, string query, int offset, int limit);
    }
}
