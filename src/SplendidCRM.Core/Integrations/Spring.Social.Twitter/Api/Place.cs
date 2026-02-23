#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class Place
    {
        public Place() { }
        public string ID { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string StreetAddress { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string PlaceType { get; set; }
    }
}
