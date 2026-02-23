#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class CommentTemplate : AbstractFacebookOperations, ICommentOperations
    {
        public CommentTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Comment> GetComments(string objectId) { requireAuthorization(); return new List<Comment>(); }
        public List<Comment> GetComments(string objectId, int offset, int limit) { requireAuthorization(); return new List<Comment>(); }
        public Comment GetComment(string commentId) { requireAuthorization(); return default(Comment); }
        public string AddComment(string objectId, string message) { requireAuthorization(); return string.Empty; }
        public void DeleteComment(string commentId) { requireAuthorization(); }
        public List<Reference> GetLikes(string objectId) { requireAuthorization(); return new List<Reference>(); }
    }
}
