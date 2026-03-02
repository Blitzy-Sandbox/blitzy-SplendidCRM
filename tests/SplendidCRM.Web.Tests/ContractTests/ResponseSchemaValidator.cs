// ResponseSchemaValidator.cs — Reusable utility for validating JSON response structures
// against expected schemas. Used by REST and Admin contract tests to verify API contracts
// match the legacy WCF API response shapes.
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SplendidCRM.Web.Tests.ContractTests
{
	/// <summary>
	/// Defines the expected type of a JSON value for schema validation.
	/// </summary>
	public enum ExpectedJsonType
	{
		String,
		Number,
		Boolean,
		Array,
		Object,
		Any  // Accepts any non-null value
	}

	/// <summary>
	/// Defines a single field requirement within a JSON response schema.
	/// </summary>
	public class FieldRequirement
	{
		/// <summary>The JSON property name (case-sensitive).</summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>The expected JSON value type.</summary>
		public ExpectedJsonType ExpectedType { get; set; }

		/// <summary>Whether the field is required (vs optional).</summary>
		public bool Required { get; set; } = true;

		/// <summary>
		/// Creates a required field requirement.
		/// </summary>
		public static FieldRequirement Require(string name, ExpectedJsonType type)
			=> new FieldRequirement { Name = name, ExpectedType = type, Required = true };

		/// <summary>
		/// Creates an optional field requirement.
		/// </summary>
		public static FieldRequirement Optional(string name, ExpectedJsonType type)
			=> new FieldRequirement { Name = name, ExpectedType = type, Required = false };
	}

	/// <summary>
	/// Validates JSON response strings against a schema defined by a list of
	/// <see cref="FieldRequirement"/> entries. Returns a list of violation messages.
	/// Reusable across all contract tests for REST and Admin endpoints.
	/// </summary>
	public static class ResponseSchemaValidator
	{
		/// <summary>
		/// Validates a JSON response string against a set of field requirements.
		/// </summary>
		/// <param name="json">The JSON response string to validate.</param>
		/// <param name="requirements">The list of field requirements to check.</param>
		/// <returns>A list of violation messages. Empty list indicates the response conforms to the schema.</returns>
		public static List<string> Validate(string json, IEnumerable<FieldRequirement> requirements)
		{
			var violations = new List<string>();

			if (string.IsNullOrWhiteSpace(json))
			{
				violations.Add("Response body is null or empty.");
				return violations;
			}

			JsonDocument doc;
			try
			{
				doc = JsonDocument.Parse(json);
			}
			catch (JsonException ex)
			{
				violations.Add($"Response is not valid JSON: {ex.Message}");
				return violations;
			}

			JsonElement root = doc.RootElement;

			foreach (var req in requirements)
			{
				if (!root.TryGetProperty(req.Name, out JsonElement element))
				{
					if (req.Required)
						violations.Add($"Required field '{req.Name}' is missing from the response.");
					continue;
				}

				if (element.ValueKind == JsonValueKind.Null)
				{
					// Null is allowed — the field exists but has a null value.
					// Only flag if the type is expected to be non-nullable and required.
					continue;
				}

				if (req.ExpectedType == ExpectedJsonType.Any)
					continue; // Accept any non-null type

				bool typeMatches = req.ExpectedType switch
				{
					ExpectedJsonType.String  => element.ValueKind == JsonValueKind.String,
					ExpectedJsonType.Number  => element.ValueKind == JsonValueKind.Number,
					ExpectedJsonType.Boolean => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
					ExpectedJsonType.Array   => element.ValueKind == JsonValueKind.Array,
					ExpectedJsonType.Object  => element.ValueKind == JsonValueKind.Object,
					_                        => true
				};

				if (!typeMatches)
				{
					violations.Add($"Field '{req.Name}' has unexpected type. Expected {req.ExpectedType}, got {element.ValueKind}.");
				}
			}

			return violations;
		}

		/// <summary>
		/// Validates that a JSON array contains objects with specific required fields.
		/// Checks the first element of the array as a representative sample.
		/// </summary>
		/// <param name="json">The JSON response containing an array.</param>
		/// <param name="arrayPath">The JSON property path to the array (e.g., "d" for {"d": [...]}).</param>
		/// <param name="elementRequirements">Field requirements for array element objects.</param>
		/// <returns>A list of violation messages.</returns>
		public static List<string> ValidateArray(string json, string arrayPath, IEnumerable<FieldRequirement> elementRequirements)
		{
			var violations = new List<string>();

			if (string.IsNullOrWhiteSpace(json))
			{
				violations.Add("Response body is null or empty.");
				return violations;
			}

			JsonDocument doc;
			try
			{
				doc = JsonDocument.Parse(json);
			}
			catch (JsonException ex)
			{
				violations.Add($"Response is not valid JSON: {ex.Message}");
				return violations;
			}

			JsonElement root = doc.RootElement;

			// Navigate to the array using the path
			if (!string.IsNullOrEmpty(arrayPath))
			{
				foreach (string segment in arrayPath.Split('.'))
				{
					if (!root.TryGetProperty(segment, out root))
					{
						violations.Add($"Array path '{arrayPath}' not found in response.");
						return violations;
					}
				}
			}

			if (root.ValueKind != JsonValueKind.Array)
			{
				violations.Add($"Expected array at path '{arrayPath}', got {root.ValueKind}.");
				return violations;
			}

			if (root.GetArrayLength() == 0)
			{
				// Empty array is valid — cannot validate element structure
				return violations;
			}

			// Validate the first element as a sample
			JsonElement firstElement = root[0];
			if (firstElement.ValueKind != JsonValueKind.Object)
			{
				violations.Add($"Array elements are expected to be objects, got {firstElement.ValueKind}.");
				return violations;
			}

			foreach (var req in elementRequirements)
			{
				if (!firstElement.TryGetProperty(req.Name, out JsonElement field))
				{
					if (req.Required)
						violations.Add($"Required field '{req.Name}' is missing from array elements.");
					continue;
				}

				if (field.ValueKind == JsonValueKind.Null || req.ExpectedType == ExpectedJsonType.Any)
					continue;

				bool typeMatches = req.ExpectedType switch
				{
					ExpectedJsonType.String  => field.ValueKind == JsonValueKind.String,
					ExpectedJsonType.Number  => field.ValueKind == JsonValueKind.Number,
					ExpectedJsonType.Boolean => field.ValueKind == JsonValueKind.True || field.ValueKind == JsonValueKind.False,
					ExpectedJsonType.Array   => field.ValueKind == JsonValueKind.Array,
					ExpectedJsonType.Object  => field.ValueKind == JsonValueKind.Object,
					_                        => true
				};

				if (!typeMatches)
				{
					violations.Add($"Array element field '{req.Name}' has unexpected type. Expected {req.ExpectedType}, got {field.ValueKind}.");
				}
			}

			return violations;
		}
	}
}
