#nullable disable
using System;
using System.Collections.Generic;

namespace Spring.Social.Facebook.Api.Impl.Json
{
    public class ListDeserializer<T> : IJsonDeserializer where T : class
    {
        private IJsonDeserializer itemDeserializer;
        public ListDeserializer(IJsonDeserializer itemDeserializer) { this.itemDeserializer = itemDeserializer; }
        public object Deserialize(JsonValue json, JsonMapper mapper) { return new List<T>(); }
    }
}
