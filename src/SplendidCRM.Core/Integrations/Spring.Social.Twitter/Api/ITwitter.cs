#nullable disable
namespace Spring.Social.Twitter.Api
{
    public interface ITwitter
    {
        ITimelineOperations TimelineOperations { get; }
        ITweetOperations TweetOperations { get; }
        IUserOperations UserOperations { get; }
        IFriendOperations FriendOperations { get; }
        IBlockOperations BlockOperations { get; }
        IDirectMessageOperations DirectMessageOperations { get; }
        ISearchOperations SearchOperations { get; }
        IListOperations ListOperations { get; }
        IGeoOperations GeoOperations { get; }
        bool IsAuthorized { get; }
    }
}
