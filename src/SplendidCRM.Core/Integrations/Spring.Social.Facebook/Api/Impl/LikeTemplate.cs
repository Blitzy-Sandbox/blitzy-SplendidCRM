#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class LikeTemplate : AbstractFacebookOperations, ILikeOperations
    {
        public LikeTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Reference> GetLikes(string objectId) { requireAuthorization(); return new List<Reference>(); }
        public List<Page> GetPagesLiked() { return GetPagesLiked("me"); }
        public List<Page> GetPagesLiked(string userId) { requireAuthorization(); return new List<Page>(); }
        public void Like(string objectId) { requireAuthorization(); Post(objectId, "likes", new NameValueCollection()); }
        public void Unlike(string objectId) { requireAuthorization(); Delete(objectId, "likes"); }
        public List<Page> GetBooks() { return GetBooks("me"); }
        public List<Page> GetBooks(string userId) { requireAuthorization(); return new List<Page>(); }
        public List<Page> GetMovies() { return GetMovies("me"); }
        public List<Page> GetMovies(string userId) { requireAuthorization(); return new List<Page>(); }
        public List<Page> GetMusic() { return GetMusic("me"); }
        public List<Page> GetMusic(string userId) { requireAuthorization(); return new List<Page>(); }
        public List<Page> GetTelevision() { return GetTelevision("me"); }
        public List<Page> GetTelevision(string userId) { requireAuthorization(); return new List<Page>(); }
        public List<Page> GetActivities() { return GetActivities("me"); }
        public List<Page> GetActivities(string userId) { requireAuthorization(); return new List<Page>(); }
        public List<Page> GetInterests() { return GetInterests("me"); }
        public List<Page> GetInterests(string userId) { requireAuthorization(); return new List<Page>(); }
        public List<Page> GetGames() { return GetGames("me"); }
        public List<Page> GetGames(string userId) { requireAuthorization(); return new List<Page>(); }
    }
}
