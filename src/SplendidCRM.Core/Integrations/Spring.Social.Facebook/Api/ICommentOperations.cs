#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface ICommentOperations
    {
        List<Comment> GetComments(string objectId);
        List<Comment> GetComments(string objectId, int offset, int limit);
        Comment GetComment(string commentId);
        string AddComment(string objectId, string message);
        void DeleteComment(string commentId);
        List<Reference> GetLikes(string objectId);
    }
}
