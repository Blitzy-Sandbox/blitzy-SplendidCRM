#nullable disable
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    public interface IGeoOperations
    {
        Place GetPlace(string placeId);
        List<Place> ReverseGeoCode(double latitude, double longitude);
        List<Place> ReverseGeoCode(double latitude, double longitude, string granularity, string accuracy);
        List<Place> Search(double latitude, double longitude);
        List<Place> Search(double latitude, double longitude, string granularity, string accuracy, int count);
        SimilarPlaces FindSimilarPlaces(double latitude, double longitude, string name);
        SimilarPlaces FindSimilarPlaces(double latitude, double longitude, string name, string streetAddress, string containedWithin);
        Place CreatePlace(string name, string containedWithin, string token, double latitude, double longitude);
    }
}
