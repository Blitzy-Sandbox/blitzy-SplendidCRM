#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl.Json
{
    public class QuestionDeserializer : IJsonDeserializer
    {
        public object Deserialize(JsonValue json, JsonMapper mapper) { return default(Question); }
    }
}
