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
    /// Stub replacement for Spring.Json.JsonValue.
    /// Represents a JSON value node used by Spring.Social deserializers.
    /// Dormant stub — not executed at runtime.
    /// </summary>
    public class JsonValue
    {
        /// <summary>Gets whether this value represents a JSON object.</summary>
        public bool IsObject { get { return false; } }

        /// <summary>Gets whether this value is null.</summary>
        public bool IsNull { get { return true; } }

        /// <summary>Gets whether this value represents a JSON array.</summary>
        public bool IsArray { get { return false; } }

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
}
