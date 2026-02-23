#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl.Json
{
    public class StoryTagMapDeserializer : IJsonDeserializer
    {
        public object Deserialize(JsonValue json, JsonMapper mapper) { return new Dictionary<int, List<StoryTag>>(); }
    }
}
