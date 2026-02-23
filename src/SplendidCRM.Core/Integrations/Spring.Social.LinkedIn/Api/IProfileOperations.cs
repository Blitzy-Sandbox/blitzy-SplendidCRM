#nullable disable
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    public interface IProfileOperations
    {
        LinkedInProfile GetUserProfile();
        LinkedInProfile GetUserProfileById(string id);
        LinkedInProfile GetUserProfileByPublicUrl(string publicProfileUrl);
        List<LinkedInProfile> Search(string query);
    }
}
