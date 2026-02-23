#nullable disable
using System;
namespace Spring.Social.Facebook.Api
{
    [Serializable]
    public class Page
    {
        public Page() { }
        public Page(string id, string name, string link, string category) { ID = id; Name = name; Link = link; Category = category; }
        public string ID { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public Location Location { get; set; }
        public string Website { get; set; }
        public string Picture { get; set; }
        public string Phone { get; set; }
        public string Affiliation { get; set; }
        public string CompanyOverview { get; set; }
        public int FanCount { get; set; }
        public int Likes { get; set; }
        public int Checkins { get; set; }
    }
}
