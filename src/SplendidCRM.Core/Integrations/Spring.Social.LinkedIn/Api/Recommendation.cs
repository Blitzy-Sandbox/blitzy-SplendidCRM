#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.LinkedIn.Api
{
    [Serializable] public class Recommendation { public string Id { get; set; } public string RecommendationText { get; set; } public RecommendationType RecommendationType { get; set; } public LinkedInProfile Recommender { get; set; } }
}
