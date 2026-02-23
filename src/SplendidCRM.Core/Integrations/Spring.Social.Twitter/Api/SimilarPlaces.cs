#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class SimilarPlaces : List<Place>
    {
        public string Token { get; set; }
    }
}
