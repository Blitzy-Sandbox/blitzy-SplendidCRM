#nullable disable
#region License

/*
 * Copyright (C) 2012 SplendidCRM Software, Inc. All Rights Reserved. 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

// .NET 10 Migration: Removed Spring.Json, Spring.Http, Spring.Rest.Client, Spring.Rest.Client.Support
// using directives. These Spring.NET Framework assemblies (discontinued) are unavailable on .NET 10.
// Equivalent stub types are defined in the #region Spring Framework Stubs block below, placed within
// the Spring.Social.Salesforce.Api.Impl namespace for full accessibility by all Impl/ subclasses
// without explicit using directives. This is a dormant integration stub — compiles but is NOT
// expected to execute at runtime.

using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// Base class for Salesforce operations.
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	abstract class AbstractSalesforceOperations : ISalesforceApi
	{
		private bool isAuthorized;
		protected RestTemplate restTemplate;

		/// <summary>
		/// Creates a new instance of <see cref="AbstractSalesforceOperations"/> with authorization state.
		/// </summary>
		/// <param name="restTemplate">The REST template to use for making HTTP requests.</param>
		/// <param name="isAuthorized">
		/// Whether the API binding was created with valid OAuth 2.0 authorization credentials.
		/// </param>
		public AbstractSalesforceOperations(RestTemplate restTemplate, bool isAuthorized) 
		{
			this.restTemplate = restTemplate;
			this.isAuthorized = isAuthorized;
		}

		// Migration note: Protected parameterless constructor added for .NET 10 compilation compatibility.
		// Dormant stub subclasses (MetadataTemplate, SObjectOperations, SearchTemplate, UserTemplate,
		// VersionTemplate) that do not define explicit constructors require an accessible parameterless
		// base constructor for the C# compiler to generate an implicit default constructor.
		// This addition enables all subclasses to compile without modification. The restTemplate field
		// is initialized to a no-op stub instance and isAuthorized defaults to false.
		protected AbstractSalesforceOperations()
		{
			this.restTemplate = new RestTemplate();
			this.isAuthorized = false;
		}

		/// <summary>
		/// Ensures that the API binding was created with authorization credentials.
		/// Delegates to <see cref="EnsureIsAuthorized"/>.
		/// </summary>
		protected void requireAuthorization()
		{
			EnsureIsAuthorized();
		}
		
		/// <summary>
		/// Throws a <see cref="SalesforceApiException"/> if the API binding was created without
		/// OAuth 2.0 authorization credentials.
		/// </summary>
		/// <exception cref="SalesforceApiException">
		/// Thrown with <see cref="SalesforceApiError.NotAuthorized"/> when the binding is unauthorized.
		/// </exception>
		protected void EnsureIsAuthorized()
		{
			if ( !this.isAuthorized )
			{
				throw new SalesforceApiException("Authorization is required for the operation, but the API binding was created without authorization.", SalesforceApiError.NotAuthorized);
			}
		}

		/// <summary>
		/// Builds a URL from the given path with no additional query parameters.
		/// </summary>
		/// <param name="path">The base path or URL.</param>
		/// <returns>The path unchanged (no query string appended).</returns>
		protected string BuildUrl(string path)
		{
			NameValueCollection parameters = new NameValueCollection();
			return this.BuildUrl(path, parameters);
		}

		/// <summary>
		/// Builds a URL from the given path with a single named query parameter.
		/// </summary>
		/// <param name="path">The base path or URL.</param>
		/// <param name="parameterName">The query parameter name (URL-encoded).</param>
		/// <param name="parameterValue">The query parameter value (URL-encoded).</param>
		/// <returns>The path with the query parameter appended, e.g. <c>path?name=value</c>.</returns>
		protected string BuildUrl(string path, string parameterName, string parameterValue)
		{
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add(parameterName, parameterValue);
			return this.BuildUrl(path, parameters);
		}

		/// <summary>
		/// Builds a URL from the given path and a collection of query parameters.
		/// Each key and value is URL-encoded using <see cref="HttpUtils.UrlEncode"/>.
		/// </summary>
		/// <param name="path">The base path or URL.</param>
		/// <param name="parameters">
		/// The query parameters to append. An empty collection returns the path unchanged.
		/// </param>
		/// <returns>
		/// The path with all query parameters appended as a query string,
		/// e.g. <c>path?key1=val1&amp;key2=val2</c>.
		/// </returns>
		protected string BuildUrl(string path, NameValueCollection parameters)
		{
			StringBuilder qsBuilder = new StringBuilder();
			bool isFirst = true;
			foreach ( string key in parameters )
			{
				if ( isFirst )
				{
					qsBuilder.Append('?');
					isFirst = false;
				}
				else
				{
					qsBuilder.Append('&');
				}
				qsBuilder.Append(HttpUtils.UrlEncode(key));
				qsBuilder.Append('=');
				qsBuilder.Append(HttpUtils.UrlEncode(parameters[key]));
			}
			return path + qsBuilder.ToString();
		}

		#region ISalesforceApi Methods

		/// <summary>
		/// Fetches an object by its Salesforce ID, deserializing it into the given type.
		/// Delegates directly to <see cref="RestTemplate.GetForObject{T}(string)"/>.
		/// </summary>
		/// <typeparam name="T">The target deserialization type.</typeparam>
		/// <param name="objectId">The Salesforce object ID or URL path.</param>
		/// <returns>The deserialized object, or <c>null</c> for this dormant stub.</returns>
		public T FetchObject<T>(string objectId) where T : class
		{
			return this.restTemplate.GetForObject<T>(objectId);
		}
	
		/// <summary>
		/// Fetches an object by its Salesforce ID with additional query parameters.
		/// Constructs the request URL via <see cref="BuildUrl(string, NameValueCollection)"/>
		/// before delegating to <see cref="RestTemplate.GetForObject{T}(string)"/>.
		/// </summary>
		/// <typeparam name="T">The target deserialization type.</typeparam>
		/// <param name="objectId">The Salesforce object ID or URL path.</param>
		/// <param name="queryParameters">Additional query parameters appended to the URL.</param>
		/// <returns>The deserialized object, or <c>null</c> for this dormant stub.</returns>
		public T FetchObject<T>(string objectId, NameValueCollection queryParameters) where T : class
		{
			return this.restTemplate.GetForObject<T>(this.BuildUrl(objectId, queryParameters));
		}

		/// <summary>
		/// Fetches connections of the specified type for the given object without field selection.
		/// Delegates to <see cref="FetchConnections{T}(string, string, string[])"/> with a null fields array.
		/// </summary>
		/// <typeparam name="T">The target element deserialization type.</typeparam>
		/// <param name="objectId">The Salesforce object ID.</param>
		/// <param name="connectionType">
		/// The connection type name appended to the object path (e.g. "friends", "albums").
		/// </param>
		/// <returns>A list of deserialized connection objects, or an empty list for this dormant stub.</returns>
		public List<T> FetchConnections<T>(string objectId, string connectionType) where T : class
		{
			return FetchConnections<T>(objectId, connectionType, (string[]) null);
		}

		/// <summary>
		/// Fetches connections of the specified type with optional field selection.
		/// Constructs a <c>fields</c> query parameter from the provided array, then delegates to
		/// <see cref="FetchConnections{T}(string, string, NameValueCollection)"/>.
		/// </summary>
		/// <typeparam name="T">The target element deserialization type.</typeparam>
		/// <param name="objectId">The Salesforce object ID.</param>
		/// <param name="connectionType">The connection type name.</param>
		/// <param name="fields">
		/// Optional array of field names to include in the response.
		/// If null or empty, no <c>fields</c> parameter is added.
		/// </param>
		/// <returns>A list of deserialized connection objects, or an empty list for this dormant stub.</returns>
		public List<T> FetchConnections<T>(string objectId, string connectionType, string[] fields) where T : class
		{
			NameValueCollection parameters = new NameValueCollection();
			if ( fields != null && fields.Length > 0)
			{
				string joinedFields = String.Join(",", fields);
				parameters.Add("fields", joinedFields);
			}
			return FetchConnections<T>(objectId, connectionType, parameters);
		}

		/// <summary>
		/// Fetches connections of the specified type using the provided query parameters.
		/// Constructs the full URL by appending the connection type path to the object ID,
		/// then delegates to <see cref="RestTemplate.GetForObject{T}(string)"/>.
		/// </summary>
		/// <typeparam name="T">The target element deserialization type.</typeparam>
		/// <param name="objectId">The Salesforce object ID.</param>
		/// <param name="connectionType">
		/// The connection type name appended as a path segment if non-empty (e.g. <c>/friends</c>).
		/// </param>
		/// <param name="queryParameters">Query parameters appended to the constructed URL.</param>
		/// <returns>A list of deserialized connection objects, or an empty list for this dormant stub.</returns>
		public List<T> FetchConnections<T>(string objectId, string connectionType, NameValueCollection queryParameters) where T : class
		{
			string connectionPath = connectionType != null && connectionType.Length > 0 ? "/" + connectionType : "";
			return this.restTemplate.GetForObject<List<T>>(this.BuildUrl(objectId + connectionPath, queryParameters));
		}

		/// <summary>
		/// Deletes the Salesforce object identified by the given ID using an HTTP DELETE request.
		/// Creates an <see cref="AcceptHeaderRequestCallback"/> and a
		/// <see cref="MessageConverterResponseExtractor{T}"/> using the template's message converters,
		/// then delegates to <see cref="RestTemplate.Execute{T}(string, HttpMethod, object, object)"/>
		/// with <see cref="HttpMethod.DELETE"/>.
		/// </summary>
		/// <typeparam name="T">The expected response type.</typeparam>
		/// <param name="objectId">The Salesforce object ID to delete.</param>
		/// <returns>The deserialized response, or <c>null</c> for this dormant stub.</returns>
		public T Delete<T>(string objectId) where T : class
		{
			AcceptHeaderRequestCallback requestCallback = new AcceptHeaderRequestCallback(typeof(T), this.restTemplate.MessageConverters);
			MessageConverterResponseExtractor<T> responseExtractor = new MessageConverterResponseExtractor<T>(this.restTemplate.MessageConverters);
			return this.restTemplate.Execute<T>(objectId, HttpMethod.DELETE, requestCallback, responseExtractor);
		}

		#endregion
	}

	#region Spring Framework Stubs

	// .NET 10 Migration: The following stub types replace Spring.NET Framework assemblies:
	//   - Spring.Http      (HttpUtils, HttpMethod, IHttpMessageConverter)
	//   - Spring.Rest.Client (RestTemplate, AcceptHeaderRequestCallback, MessageConverterResponseExtractor<T>)
	//   - Spring.Json      (IJsonDeserializer, JsonValue, JsonMapper)
	//
	// These assemblies are discontinued with no .NET Core / .NET 10 equivalent NuGet packages.
	// All stubs are defined within the Spring.Social.Salesforce.Api.Impl namespace so that
	// AbstractSalesforceOperations and all Impl/ subclasses (MetadataTemplate, SObjectOperations,
	// SearchTemplate, UserTemplate, VersionTemplate) can resolve them without using directives.
	// The stubs satisfy compilation requirements but are NOT executed at runtime (dormant stub pattern,
	// AAP section 0.7.4).

	/// <summary>
	/// Stub replacement for <c>Spring.Http.HttpUtils</c> (from the discontinued Spring.Rest.dll).
	/// Provides URL and form encoding utility methods consumed by
	/// <see cref="AbstractSalesforceOperations.BuildUrl(string, NameValueCollection)"/>.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public static class HttpUtils
	{
		/// <summary>
		/// URL-encodes the given value using RFC 3986 percent-encoding
		/// (<see cref="Uri.EscapeDataString"/>).
		/// </summary>
		/// <param name="value">The string to encode. If <c>null</c>, treated as empty string.</param>
		/// <returns>The percent-encoded string safe for use in a URL query component.</returns>
		public static string UrlEncode(string value)
		{
			return Uri.EscapeDataString(value ?? string.Empty);
		}

		/// <summary>
		/// Form-encodes the given value (equivalent to URL encoding for this stub implementation).
		/// </summary>
		/// <param name="value">The string to encode. If <c>null</c>, treated as empty string.</param>
		/// <returns>The percent-encoded string safe for use in a URL-encoded form body.</returns>
		public static string FormEncode(string value)
		{
			return Uri.EscapeDataString(value ?? string.Empty);
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Http.HttpMethod</c> (from the discontinued Spring.Rest.dll).
	/// Provides HTTP method constants used in
	/// <see cref="AbstractSalesforceOperations.Delete{T}(string)"/> via
	/// <see cref="RestTemplate.Execute{T}(string, HttpMethod, object, object)"/>.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class HttpMethod
	{
		/// <summary>HTTP DELETE method constant used for delete operations.</summary>
		public static readonly HttpMethod DELETE = new HttpMethod();

		/// <summary>HTTP GET method constant used for fetch operations.</summary>
		public static readonly HttpMethod GET = new HttpMethod();

		/// <summary>HTTP POST method constant used for create/update operations.</summary>
		public static readonly HttpMethod POST = new HttpMethod();

		// Private constructor — consumers must use the predefined static constants.
		private HttpMethod() { }
	}

	/// <summary>
	/// Marker interface replacing <c>Spring.Http.Converter.IHttpMessageConverter</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Used as the element type of <see cref="RestTemplate.MessageConverters"/>,
	/// <see cref="AcceptHeaderRequestCallback"/>, and <see cref="MessageConverterResponseExtractor{T}"/>.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public interface IHttpMessageConverter { }

	/// <summary>
	/// Stub replacement for <c>Spring.Rest.Client.RestTemplate</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Provides HTTP operation methods consumed by <see cref="AbstractSalesforceOperations"/>:
	/// <see cref="GetForObject{T}(string)"/> for fetch operations and
	/// <see cref="Execute{T}(string, HttpMethod, object, object)"/> for delete operations.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class RestTemplate
	{
		/// <summary>
		/// Gets or sets the message converters used to serialize request bodies and
		/// deserialize response bodies. Initialized to an empty list.
		/// </summary>
		public IList<IHttpMessageConverter> MessageConverters { get; set; } = new List<IHttpMessageConverter>();

		/// <summary>
		/// Gets or sets the base URI that request URLs are resolved relative to.
		/// </summary>
		public Uri BaseAddress { get; set; }

		/// <summary>
		/// Gets the list of request interceptors applied before each outgoing request.
		/// Initialized to an empty list.
		/// </summary>
		public IList<object> RequestInterceptors { get; } = new List<object>();

		/// <summary>
		/// Gets or sets the request factory used to create the underlying HTTP request objects.
		/// Typed as <c>object</c> to avoid dependency on the discontinued Spring.Http.Client assembly.
		/// </summary>
		public object RequestFactory { get; set; }

		/// <summary>
		/// Gets or sets the error handler that processes HTTP error responses.
		/// Typed as <c>object</c> to avoid dependency on the discontinued Spring.Rest.Client assembly.
		/// </summary>
		public object ErrorHandler { get; set; }

		/// <summary>
		/// Stub: Performs an HTTP GET request and deserializes the response into type <typeparamref name="T"/>.
		/// Dormant stub — returns <c>default(T)</c> without making a network call.
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response body as.</typeparam>
		/// <param name="url">The URL to send the GET request to.</param>
		/// <returns>
		/// <c>default(T)</c> — dormant stub. At runtime this would return the deserialized response.
		/// </returns>
		public T GetForObject<T>(string url)
		{
			return default(T);
		}

		/// <summary>
		/// Stub: Performs an HTTP POST request with the given body and deserializes the response.
		/// Dormant stub — returns <c>default(T)</c> without making a network call.
		/// </summary>
		/// <typeparam name="T">The type to deserialize the response body as.</typeparam>
		/// <param name="url">The URL to send the POST request to.</param>
		/// <param name="body">The request body to serialize and send.</param>
		/// <returns>
		/// <c>default(T)</c> — dormant stub. At runtime this would return the deserialized response.
		/// </returns>
		public T PostForObject<T>(string url, object body)
		{
			return default(T);
		}

		/// <summary>
		/// Stub: Performs an HTTP DELETE request.
		/// Dormant stub — no-op, makes no network call.
		/// </summary>
		/// <param name="url">The URL to send the DELETE request to.</param>
		public void Delete(string url)
		{
			// Dormant stub — no-op. At runtime this would send an HTTP DELETE request.
		}

		/// <summary>
		/// Stub: Executes an HTTP request using the specified method, applying the given request
		/// callback before sending and using the response extractor to deserialize the response.
		/// Dormant stub — returns <c>default(T)</c> without making a network call.
		/// </summary>
		/// <typeparam name="T">The expected response type.</typeparam>
		/// <param name="url">The request URL.</param>
		/// <param name="method">The HTTP method to use (e.g. <see cref="HttpMethod.DELETE"/>).</param>
		/// <param name="requestCallback">
		/// The callback invoked to configure the request before it is sent (unused in stub).
		/// </param>
		/// <param name="responseExtractor">
		/// The extractor used to deserialize the response body (unused in stub).
		/// </param>
		/// <returns>
		/// <c>default(T)</c> — dormant stub. At runtime this would return the extracted response.
		/// </returns>
		public T Execute<T>(string url, HttpMethod method, object requestCallback, object responseExtractor)
		{
			return default(T);
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Rest.Client.Support.AcceptHeaderRequestCallback</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Configures the HTTP request <c>Accept</c> header based on the media types supported by
	/// the registered message converters for the specified response type.
	/// Used in <see cref="AbstractSalesforceOperations.Delete{T}(string)"/> to negotiate response format.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class AcceptHeaderRequestCallback
	{
		/// <summary>
		/// Creates a new instance of <see cref="AcceptHeaderRequestCallback"/>.
		/// </summary>
		/// <param name="responseType">
		/// The expected response type used to select compatible message converters for Accept negotiation.
		/// </param>
		/// <param name="messageConverters">
		/// The list of message converters to inspect for supported media types.
		/// </param>
		public AcceptHeaderRequestCallback(Type responseType, IList<IHttpMessageConverter> messageConverters)
		{
			// Dormant stub — no-op constructor. At runtime this would inspect message converters
			// to populate the Accept header with the appropriate MIME types.
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Rest.Client.Support.MessageConverterResponseExtractor{T}</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Extracts the HTTP response body by delegating to the first message converter that can read
	/// the response content type as type <typeparamref name="T"/>.
	/// Used in <see cref="AbstractSalesforceOperations.Delete{T}(string)"/> to process the response.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	/// <typeparam name="T">The type to extract the response body as.</typeparam>
	public class MessageConverterResponseExtractor<T>
	{
		/// <summary>
		/// Creates a new instance of <see cref="MessageConverterResponseExtractor{T}"/>.
		/// </summary>
		/// <param name="messageConverters">
		/// The list of message converters to use for response body deserialization.
		/// </param>
		public MessageConverterResponseExtractor(IList<IHttpMessageConverter> messageConverters)
		{
			// Dormant stub — no-op constructor. At runtime this would hold a reference to the
			// message converters for later use when extracting the response body.
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Json.IJsonDeserializer</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Interface for type-specific JSON deserialization dispatch within the Spring.Social
	/// JSON infrastructure. Implemented by Salesforce deserializer classes in
	/// <c>Spring.Social.Salesforce.Api.Impl.Json</c>.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public interface IJsonDeserializer
	{
		/// <summary>
		/// Deserializes the given JSON value into a .NET object using the provided mapper
		/// for nested type dispatch.
		/// </summary>
		/// <param name="json">The JSON value node to deserialize.</param>
		/// <param name="mapper">
		/// The JSON mapper providing type-dispatched deserialization for nested objects.
		/// </param>
		/// <returns>The deserialized .NET object.</returns>
		object Deserialize(JsonValue json, JsonMapper mapper);
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Json.JsonValue</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Represents a JSON value node that may be an object, array, string, number, boolean, or null.
	/// Used by Salesforce deserializer classes to navigate and extract JSON API response data.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class JsonValue
	{
		/// <summary>
		/// Initializes a new empty <see cref="JsonValue"/> stub instance.
		/// Dormant stub — not executed at runtime.
		/// </summary>
		public JsonValue() { }

		/// <summary>Gets whether this value represents JSON <c>null</c>. Always <c>true</c> in stub.</summary>
		public bool IsNull { get { return true; } }

		/// <summary>Gets whether this value represents a JSON array. Always <c>false</c> in stub.</summary>
		public bool IsArray { get { return false; } }

		/// <summary>Gets whether this value represents a JSON object. Always <c>false</c> in stub.</summary>
		public bool IsObject { get { return false; } }

		/// <summary>Gets whether this value represents a JSON string. Always <c>false</c> in stub.</summary>
		public bool IsString { get { return false; } }

		/// <summary>Gets whether this value represents a JSON boolean. Always <c>false</c> in stub.</summary>
		public bool IsBoolean { get { return false; } }

		/// <summary>Gets whether this value represents a JSON number. Always <c>false</c> in stub.</summary>
		public bool IsNumber { get { return false; } }

		/// <summary>
		/// Returns whether this JSON object contains a child property with the specified name.
		/// Always <c>false</c> in stub.
		/// </summary>
		/// <param name="name">The property name to check for.</param>
		/// <returns><c>false</c> — dormant stub.</returns>
		public bool ContainsName(string name) { return false; }

		/// <summary>
		/// Gets the typed value of this JSON scalar node (e.g. string, int, bool).
		/// Returns <c>default(T)</c> in stub.
		/// </summary>
		/// <typeparam name="T">The target type.</typeparam>
		/// <returns><c>default(T)</c> — dormant stub.</returns>
		public T GetValue<T>() { return default(T); }

		/// <summary>
		/// Gets the typed child value of this JSON object node by property name.
		/// Returns <c>default(T)</c> in stub.
		/// </summary>
		/// <typeparam name="T">The target type.</typeparam>
		/// <param name="name">The child property name.</param>
		/// <returns><c>default(T)</c> — dormant stub.</returns>
		public T GetValue<T>(string name) { return default(T); }

		/// <summary>
		/// Gets a child JSON value node by property name.
		/// Returns a new empty <see cref="JsonValue"/> in stub.
		/// </summary>
		/// <param name="name">The child property name.</param>
		/// <returns>A new empty <see cref="JsonValue"/> instance — dormant stub.</returns>
		public JsonValue GetValue(string name) { return new JsonValue(); }

		/// <summary>
		/// Gets a JSON array element by zero-based index.
		/// Returns a new empty <see cref="JsonValue"/> in stub.
		/// </summary>
		/// <param name="index">The zero-based array index.</param>
		/// <returns>A new empty <see cref="JsonValue"/> instance — dormant stub.</returns>
		public JsonValue GetValue(int index) { return new JsonValue(); }

		/// <summary>
		/// Gets all child JSON values of this array node for iteration.
		/// Returns an empty enumerable in stub.
		/// </summary>
		/// <returns>An empty <see cref="IEnumerable{JsonValue}"/> — dormant stub.</returns>
		public IEnumerable<JsonValue> GetValues() { return new List<JsonValue>(); }

		/// <summary>
		/// Gets all child property names of this JSON object node.
		/// Returns an empty enumerable in stub.
		/// </summary>
		/// <returns>An empty <see cref="IEnumerable{String}"/> — dormant stub.</returns>
		public IEnumerable<string> GetNames() { return new List<string>(); }

		/// <summary>
		/// Attempts to parse a JSON string into a <see cref="JsonValue"/> node.
		/// Always returns <c>false</c> in stub; <paramref name="result"/> is set to a new empty instance.
		/// </summary>
		/// <param name="json">The JSON string to parse.</param>
		/// <param name="result">
		/// When this method returns, contains a new empty <see cref="JsonValue"/> instance.
		/// </param>
		/// <returns><c>false</c> — dormant stub always fails to parse.</returns>
		public static bool TryParse(string json, out JsonValue result)
		{
			result = new JsonValue();
			return false;
		}
	}

	/// <summary>
	/// Stub replacement for <c>Spring.Json.JsonMapper</c>
	/// (from the discontinued Spring.Rest.dll).
	/// Provides type-dispatched JSON deserialization using registered
	/// <see cref="IJsonDeserializer"/> instances. Used by Salesforce deserializer classes to
	/// recursively deserialize nested JSON objects.
	/// Dormant stub — not executed at runtime.
	/// </summary>
	public class JsonMapper
	{
		/// <summary>
		/// Registers a deserializer implementation for the specified .NET type.
		/// No-op in stub.
		/// </summary>
		/// <param name="type">The target .NET type this deserializer handles.</param>
		/// <param name="deserializer">
		/// The <see cref="IJsonDeserializer"/> implementation to register.
		/// </param>
		public void RegisterDeserializer(Type type, IJsonDeserializer deserializer)
		{
			// Dormant stub — no-op. At runtime this would register the deserializer for type dispatch.
		}

		/// <summary>
		/// Deserializes the given JSON value into an instance of type <typeparamref name="T"/>
		/// using the registered deserializer for that type.
		/// </summary>
		/// <typeparam name="T">The target deserialization type.</typeparam>
		/// <param name="value">The JSON value node to deserialize.</param>
		/// <returns>Never returns — dormant stub always throws.</returns>
		/// <exception cref="NotImplementedException">
		/// Always thrown — this Salesforce integration is a dormant stub on .NET 10.
		/// No deserializers are registered and this method is not expected to be called at runtime.
		/// In production, a registered <see cref="IJsonDeserializer"/> would handle the conversion.
		/// </exception>
		public T Deserialize<T>(JsonValue value)
		{
			// Dormant stub — Spring.Json.JsonMapper is unavailable on .NET 10.
			// Throw NotImplementedException to clearly identify unintended invocations.
			// TECHNICAL DEBT: Re-implement with System.Text.Json or Newtonsoft.Json when
			// the Salesforce integration is activated for production use.
			throw new NotImplementedException(
				"Dormant stub: JsonMapper.Deserialize is not implemented for .NET 10. " +
				"The Salesforce integration (Spring.Social.Salesforce) is a dormant Enterprise Edition stub. " +
				"Type requested: " + typeof(T).FullName);
		}
	}

	#endregion
}
