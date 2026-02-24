/*******************************************************************************************************************
 * Spring.Json stub — Minimal replacement for Spring.Json types from the discontinued Spring.NET library.
 * Created as part of .NET Framework 4.8 → .NET 10 migration (AAP section 0.7.4).
 * These stubs satisfy compilation requirements for dormant Spring.Social.* integration files.
 * They are NOT intended for production execution.
 *******************************************************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spring.Json
{
    /// <summary>
    /// Stub replacement for Spring.Json.IJsonDeserializer.
    /// Interface for JSON deserialization used by Spring.Social integration stubs.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public interface IJsonDeserializer
    {
        /// <summary>
        /// Deserializes a JSON value using the provided mapper.
        /// </summary>
        object Deserialize(JsonValue value, JsonMapper mapper);
    }

    /// <summary>
    /// Stub replacement for Spring.Json.IJsonSerializer.
    /// Interface for JSON serialization used by Spring.Social integration stubs.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public interface IJsonSerializer
    {
        /// <summary>
        /// Serializes the given object into a JSON value using the provided mapper.
        /// </summary>
        JsonValue Serialize(object obj, JsonMapper mapper);
    }

    /// <summary>
    /// Stub replacement for Spring.Json.JsonValue.
    /// Represents a JSON value node used by Spring.Social deserializers.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class JsonValue
    {
        /// <summary>
        /// Initializes a new empty JsonValue stub.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public JsonValue() { }

        /// <summary>
        /// Initializes a new JsonValue stub with a string value.
        /// Supports Spring.Social serializers that create JsonValue nodes from string data.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public JsonValue(string value) { }
        /// <summary>Gets whether this value represents a JSON object.</summary>
        public bool IsObject { get { return false; } }

        /// <summary>Gets whether this value is null.</summary>
        public bool IsNull { get { return true; } }

        /// <summary>Gets whether this value represents a JSON array.</summary>
        public bool IsArray { get { return false; } }

        /// <summary>Gets whether this value represents a JSON string.</summary>
        public bool IsString { get { return false; } }

        /// <summary>
        /// Attempts to parse a JSON string into a JsonValue.
        /// Stub implementation — always returns false. Dormant stub.
        /// </summary>
        public static bool TryParse(string json, out JsonValue result)
        {
            result = null;
            return false;
        }

        /// <summary>
        /// Gets the typed value of this JSON node.
        /// </summary>
        public T GetValue<T>()
        {
            return default(T)!;
        }

        /// <summary>
        /// Gets a child JSON value by name, returning the typed result.
        /// </summary>
        public T GetValue<T>(string name)
        {
            return default(T)!;
        }

        /// <summary>
        /// Gets a JSON value at the specified array index.
        /// Supports dormant Spring.Social deserializers that access array elements by position.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public JsonValue GetValue(int index)
        {
            return new JsonValue();
        }

        /// <summary>
        /// Gets a typed value by integer index (for JSON array access).
        /// Supports dormant Spring.Social deserializers that access array elements by position.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public T GetValue<T>(int index)
        {
            return default(T)!;
        }

        /// <summary>
        /// Gets a child JSON value by name.
        /// </summary>
        public JsonValue GetValue(string name)
        {
            return new JsonValue();
        }

        /// <summary>
        /// Gets a typed child value by name, returning a default if not present.
        /// </summary>
        public T GetValueOrDefault<T>(string name)
        {
            return default(T)!;
        }

        /// <summary>
        /// Gets a typed child value by name, returning the specified default if not present.
        /// </summary>
        public T GetValueOrDefault<T>(string name, T defaultValue)
        {
            return defaultValue;
        }

        /// <summary>
        /// Gets all child JSON values (for array iteration).
        /// </summary>
        public IList<JsonValue> GetValues()
        {
            return new List<JsonValue>();
        }

        /// <summary>
        /// Gets child JSON values by name (for named array iteration).
        /// </summary>
        public IList<JsonValue> GetValues(string name)
        {
            return new List<JsonValue>();
        }

        /// <summary>
        /// Checks whether this JSON object contains a named child.
        /// </summary>
        public bool ContainsName(string name)
        {
            return false;
        }

        /// <summary>
        /// Gets all child property names of this JSON object.
        /// </summary>
        public IEnumerable<string> GetNames()
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Stub replacement for Spring.Json.JsonMapper.
    /// Provides type-safe JSON deserialization dispatch.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class JsonMapper
    {
        /// <summary>
        /// Deserializes a JSON value into the specified type.
        /// </summary>
        public T Deserialize<T>(JsonValue value)
        {
            return default(T)!;
        }
    }

    /// <summary>
    /// Stub replacement for Spring.Json.JsonObject.
    /// Represents a JSON object node with named child values.
    /// Used by Spring.Social serializers to build JSON request payloads.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class JsonObject : JsonValue
    {
        /// <summary>
        /// Initializes a new empty JsonObject stub.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public JsonObject() : base(null!) { }

        /// <summary>
        /// Adds a named JSON value to this object.
        /// Supports Spring.Social serializers building JSON object trees.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public void AddValue(string name, JsonValue value) { }
    }

    /// <summary>
    /// Stub replacement for Spring.Json.JsonArray.
    /// Represents a JSON array node containing ordered JSON values.
    /// Used by Spring.Social serializers to build JSON array payloads.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class JsonArray : JsonValue
    {
        /// <summary>
        /// Initializes a new empty JsonArray stub.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public JsonArray() : base(null!) { }

        /// <summary>
        /// Appends a JSON value to this array.
        /// Supports Spring.Social serializers building JSON arrays of objects.
        /// Dormant stub — not executed at runtime.
        /// </summary>
        public void AddValue(JsonValue value) { }
    }
}
