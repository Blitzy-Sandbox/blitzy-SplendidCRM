#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IPageOperations
    {
        Page GetPage(string pageId);
        bool IsPageAdmin(string pageId);
        List<Account> GetAccounts();
        string Post(string pageId, string message);
        string Post(string pageId, string message, FacebookLink link);
        string PostPhoto(string pageId, string albumId, Resource photo);
        string PostPhoto(string pageId, string albumId, Resource photo, string caption);
    }
}
