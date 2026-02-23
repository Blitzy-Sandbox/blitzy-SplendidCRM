#nullable disable
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
    public class PlacesTemplate : AbstractFacebookOperations, IPlacesOperations
    {
        public PlacesTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized) : base(applicationNamespace, restTemplate, isAuthorized) { }
        public List<Checkin> GetCheckins() { requireAuthorization(); return new List<Checkin>(); }
        public List<Checkin> GetCheckins(int offset, int limit) { requireAuthorization(); return new List<Checkin>(); }
        public List<Checkin> GetCheckins(string objectId) { requireAuthorization(); return new List<Checkin>(); }
        public List<Checkin> GetCheckins(string objectId, int offset, int limit) { requireAuthorization(); return new List<Checkin>(); }
        public Checkin GetCheckin(string checkinId) { requireAuthorization(); return default(Checkin); }
        public string Checkin(string placeId, double latitude, double longitude) { requireAuthorization(); return string.Empty; }
        public string Checkin(string placeId, double latitude, double longitude, string message, string[] tags) { requireAuthorization(); return string.Empty; }
        public List<Page> Search(string query, double latitude, double longitude, long distance) { requireAuthorization(); return new List<Page>(); }
    }
}
