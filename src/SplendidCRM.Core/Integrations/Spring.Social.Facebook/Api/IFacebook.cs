#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    public interface IApiBinding { bool IsAuthorized { get; } }
    public interface IRestOperations { }
    public interface IFacebook : IApiBinding
    {
        IUserOperations UserOperations { get; }
        IPlacesOperations PlacesOperations { get; }
        ILikeOperations LikeOperations { get; }
        IFriendOperations FriendOperations { get; }
        IFeedOperations FeedOperations { get; }
        IGroupOperations GroupOperations { get; }
        ICommentOperations CommentOperations { get; }
        IEventOperations EventOperations { get; }
        IMediaOperations MediaOperations { get; }
        IPageOperations PageOperations { get; }
        IFqlOperations FqlOperations { get; }
        IQuestionOperations QuestionOperations { get; }
        IOpenGraphOperations OpenGraphOperations { get; }
        IRestOperations RestOperations { get; }
    }
}
