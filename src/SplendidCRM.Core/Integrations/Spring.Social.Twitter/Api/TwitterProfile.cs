#nullable disable
using System;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class TwitterProfile
    {
        public TwitterProfile() { }
        public TwitterProfile(long id, string screenName, string name, string url, string profileImageUrl, string description, DateTime createdDate)
        { ID = id; ScreenName = screenName; Name = name; Url = url; ProfileImageUrl = profileImageUrl; Description = description; CreatedDate = createdDate; }
        public long ID { get; set; }
        public string IdStr { get; set; }
        public string ScreenName { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string ProfileImageUrl { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Language { get; set; }
        public int StatusesCount { get; set; }
        public int FriendsCount { get; set; }
        public int FollowersCount { get; set; }
        public int FavoritesCount { get; set; }
        public int ListedCount { get; set; }
        public bool Following { get; set; }
        public bool FollowRequestSent { get; set; }
        public bool IsProtected { get; set; }
        public bool NotificationsEnabled { get; set; }
        public bool Verified { get; set; }
        public bool GeoEnabled { get; set; }
        public bool ContributorsEnabled { get; set; }
        public bool IsTranslator { get; set; }
        public string TimeZone { get; set; }
        public int UtcOffset { get; set; }
        public string SidebarBorderColor { get; set; }
        public string SidebarFillColor { get; set; }
        public string BackgroundColor { get; set; }
        public bool UseBackgroundImage { get; set; }
        public string BackgroundImageUrl { get; set; }
        public bool BackgroundImageTiled { get; set; }
        public string TextColor { get; set; }
        public string LinkColor { get; set; }
        public bool ShowAllInlineMedia { get; set; }
        public string ProfileBannerUrl { get; set; }
    }
}
