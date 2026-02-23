#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Twitter.Api;

namespace Spring.Social.Twitter.Api.Impl
{
    class GeoTemplate : AbstractTwitterOperations, IGeoOperations
    {
        public Place GetPlace(string placeId) { return null; }
        public List<Place> ReverseGeoCode(double latitude, double longitude) { return new List<Place>(); }
        public List<Place> ReverseGeoCode(double latitude, double longitude, string granularity, string accuracy) { return new List<Place>(); }
        public List<Place> Search(double latitude, double longitude) { return new List<Place>(); }
        public List<Place> Search(double latitude, double longitude, string granularity, string accuracy, int count) { return new List<Place>(); }
        public SimilarPlaces FindSimilarPlaces(double latitude, double longitude, string name) { return null; }
        public SimilarPlaces FindSimilarPlaces(double latitude, double longitude, string name, string streetAddress, string containedWithin) { return null; }
        public Place CreatePlace(string name, string containedWithin, string token, double latitude, double longitude) { return null; }
    }
}
