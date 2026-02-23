#nullable disable
using System;
using System.IO;
using System.Collections.Generic;
namespace Spring.Social.Facebook.Api
{
    public interface IPlacesOperations
    {
        List<Checkin> GetCheckins();
        List<Checkin> GetCheckins(int offset, int limit);
        List<Checkin> GetCheckins(string objectId);
        List<Checkin> GetCheckins(string objectId, int offset, int limit);
        Checkin GetCheckin(string checkinId);
        string Checkin(string placeId, double latitude, double longitude);
        string Checkin(string placeId, double latitude, double longitude, string message, string[] tags);
        List<Page> Search(string query, double latitude, double longitude, long distance);
    }
}
