#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class Trend
    {
        public Trend() { }
        public Trend(string name, string query) { Name = name; Query = query; }
        public string Name { get; set; }
        public string Query { get; set; }
    }

    [Serializable]
    public class Trends
    {
        public Trends() { }
        public DateTime? AsOf { get; set; }
        public List<Trend> TrendList { get; set; }
    }
}
