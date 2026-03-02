// SqlTests.cs — Unit tests for SplendidCRM.Sql static utility methods.
// Tests pure/static methods that do NOT require database access.
using System;
using System.Data;
using Xunit;
using FluentAssertions;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class SqlTests
	{
		// =====================================================================
		// EscapeSQL
		// =====================================================================
		[Theory]
		[InlineData(null, "")]
		[InlineData("", "")]
		[InlineData("hello", "hello")]
		[InlineData("it's", "it''s")]
		[InlineData("they''re", "they''''re")]
		public void EscapeSQL_ShouldEscapeSingleQuotes(string? input, string expected)
		{
			// Sql.EscapeSQL requires non-null; null → empty is handled by Sql.ToString
			string safeInput = input ?? string.Empty;
			string result = Sql.EscapeSQL(safeInput);
			result.Should().Be(expected);
		}

		// =====================================================================
		// EscapeSQLLike
		// =====================================================================
		[Theory]
		[InlineData("hello", "hello")]
		[InlineData("100%", @"100\%")]
		[InlineData("under_score", @"under\_score")]
		public void EscapeSQLLike_ShouldEscapeWildcards(string input, string expected)
		{
			string result = Sql.EscapeSQLLike(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void EscapeSQLLike_BracketNotEscaped()
		{
			// The method does NOT escape [ brackets — only %, _, and \
			string result = Sql.EscapeSQLLike("[bracket]");
			result.Should().Be("[bracket]");
		}

		[Fact]
		public void EscapeSQLLike_BackslashEscaped()
		{
			string result = Sql.EscapeSQLLike(@"path\to\file");
			result.Should().Be(@"path\\to\\file");
		}

		// =====================================================================
		// EscapeXml
		// =====================================================================
		[Theory]
		[InlineData("hello", "hello")]
		public void EscapeXml_PassthroughPlainText(string input, string expected)
		{
			string result = Sql.EscapeXml(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void EscapeXml_EscapesAngleBrackets()
		{
			// Note: EscapeXml replaces & LAST, so &lt; from < replacement becomes &amp;lt;
			// This is the actual (legacy-preserved) behavior of the method.
			string result = Sql.EscapeXml("<tag>");
			result.Should().Contain("lt;");
			result.Should().Contain("gt;");
		}

		[Fact]
		public void EscapeXml_EscapesQuotes()
		{
			string result = Sql.EscapeXml("a\"b");
			result.Should().Contain("quot;");
		}

		[Fact]
		public void EscapeXml_EscapesApostrophe()
		{
			string result = Sql.EscapeXml("a'b");
			result.Should().Contain("apos;");
		}

		[Fact]
		public void EscapeXml_AmpersandEscaped()
		{
			string result = Sql.EscapeXml("A&B");
			result.Should().Contain("&amp;");
		}

		// =====================================================================
		// EscapeJavaScript
		// =====================================================================
		[Theory]
		[InlineData("hello", "hello")]
		[InlineData("it's", "it\\'s")]
		[InlineData("say \"hi\"", "say \\\"hi\\\"")]
		public void EscapeJavaScript_ShouldEscapeSpecialChars(string input, string expected)
		{
			string result = Sql.EscapeJavaScript(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void EscapeJavaScript_EscapesNewlines()
		{
			string result = Sql.EscapeJavaScript("line1\nline2\rline3\ttab");
			result.Should().Contain("\\n");
			result.Should().Contain("\\r");
			result.Should().Contain("\\t");
		}

		[Fact]
		public void EscapeJavaScript_ObjectOverload_HandlesNull()
		{
			string result = Sql.EscapeJavaScript((object?)null);
			result.Should().BeEmpty();
		}

		// =====================================================================
		// IsEmptyString
		// =====================================================================
		[Theory]
		[InlineData(null, true)]
		[InlineData("", true)]
		[InlineData("hello", false)]
		public void IsEmptyString_String_ShouldDetectEmpty(string? input, bool expected)
		{
			bool result = Sql.IsEmptyString(input);
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData(null, true)]
		[InlineData("", true)]
		[InlineData("hello", false)]
		public void IsEmptyString_Object_ShouldDetectEmpty(object? input, bool expected)
		{
			bool result = Sql.IsEmptyString(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void IsEmptyString_Object_DBNull_ReturnsTrue()
		{
			bool result = Sql.IsEmptyString(DBNull.Value);
			result.Should().BeTrue();
		}

		// =====================================================================
		// IsEmptyGuid
		// =====================================================================
		[Fact]
		public void IsEmptyGuid_EmptyGuid_ReturnsTrue()
		{
			Sql.IsEmptyGuid(Guid.Empty).Should().BeTrue();
		}

		[Fact]
		public void IsEmptyGuid_NewGuid_ReturnsFalse()
		{
			Sql.IsEmptyGuid(Guid.NewGuid()).Should().BeFalse();
		}

		[Fact]
		public void IsEmptyGuid_NullObject_ReturnsTrue()
		{
			Sql.IsEmptyGuid((object?)null).Should().BeTrue();
		}

		[Fact]
		public void IsEmptyGuid_DBNullObject_ReturnsTrue()
		{
			Sql.IsEmptyGuid(DBNull.Value).Should().BeTrue();
		}

		[Fact]
		public void IsEmptyGuid_ValidGuidString_ReturnsFalse()
		{
			Guid g = Guid.NewGuid();
			Sql.IsEmptyGuid((object)g.ToString()).Should().BeFalse();
		}

		// =====================================================================
		// ToGuid
		// =====================================================================
		[Fact]
		public void ToGuid_ValidGuidString_ReturnsGuid()
		{
			Guid expected = Guid.NewGuid();
			Guid result = Sql.ToGuid((object)expected.ToString());
			result.Should().Be(expected);
		}

		[Fact]
		public void ToGuid_NullObject_ReturnsEmpty()
		{
			Sql.ToGuid((object?)null).Should().Be(Guid.Empty);
		}

		[Fact]
		public void ToGuid_InvalidString_ReturnsEmpty()
		{
			Sql.ToGuid((object)"not-a-guid").Should().Be(Guid.Empty);
		}

		[Fact]
		public void ToGuid_DBNull_ReturnsEmpty()
		{
			Sql.ToGuid(DBNull.Value).Should().Be(Guid.Empty);
		}

		[Fact]
		public void ToGuid_EmptyString_ReturnsEmpty()
		{
			Sql.ToGuid("").Should().Be(Guid.Empty);
		}

		[Fact]
		public void ToGuid_NullString_ReturnsEmpty()
		{
			Sql.ToGuid((string?)null).Should().Be(Guid.Empty);
		}

		// =====================================================================
		// ToInteger
		// =====================================================================
		[Theory]
		[InlineData(null, 0)]
		[InlineData("", 0)]
		[InlineData("42", 42)]
		[InlineData("-1", -1)]
		[InlineData("abc", 0)]
		public void ToInteger_String_ShouldConvert(string? input, int expected)
		{
			int result = Sql.ToInteger(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void ToInteger_Object_Null_ReturnsZero()
		{
			Sql.ToInteger((object?)null).Should().Be(0);
		}

		[Fact]
		public void ToInteger_Object_DBNull_ReturnsZero()
		{
			Sql.ToInteger(DBNull.Value).Should().Be(0);
		}

		[Fact]
		public void ToInteger_Object_ValidInt_ReturnsValue()
		{
			Sql.ToInteger((object)"123").Should().Be(123);
		}

		// =====================================================================
		// ToDecimal
		// =====================================================================
		[Fact]
		public void ToDecimal_Null_ReturnsZero()
		{
			Sql.ToDecimal((object?)null).Should().Be(0m);
		}

		[Fact]
		public void ToDecimal_DBNull_ReturnsZero()
		{
			Sql.ToDecimal(DBNull.Value).Should().Be(0m);
		}

		[Fact]
		public void ToDecimal_ValidString_ReturnsDecimal()
		{
			Sql.ToDecimal("42.5").Should().Be(42.5m);
		}

		[Fact]
		public void ToDecimal_InvalidString_ReturnsZero()
		{
			Sql.ToDecimal("abc").Should().Be(0m);
		}

		[Fact]
		public void ToDecimal_EmptyString_ReturnsZero()
		{
			Sql.ToDecimal("").Should().Be(0m);
		}

		// =====================================================================
		// ToBoolean
		// =====================================================================
		[Theory]
		[InlineData(null, false)]
		[InlineData("", false)]
		[InlineData("true", true)]
		[InlineData("True", true)]
		[InlineData("1", true)]
		[InlineData("on", true)]
		[InlineData("false", false)]
		[InlineData("0", false)]
		[InlineData("abc", false)]
		public void ToBoolean_String_ShouldConvert(string? input, bool expected)
		{
			bool result = Sql.ToBoolean(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void ToBoolean_Object_Null_ReturnsFalse()
		{
			Sql.ToBoolean((object?)null).Should().BeFalse();
		}

		[Fact]
		public void ToBoolean_Object_DBNull_ReturnsFalse()
		{
			Sql.ToBoolean(DBNull.Value).Should().BeFalse();
		}

		[Fact]
		public void ToBoolean_Object_BoolTrue_ReturnsTrue()
		{
			Sql.ToBoolean((object)true).Should().BeTrue();
		}

		// =====================================================================
		// ToDateTime
		// =====================================================================
		[Fact]
		public void ToDateTime_NullString_ReturnsMinValue()
		{
			Sql.ToDateTime((string?)null).Should().Be(DateTime.MinValue);
		}

		[Fact]
		public void ToDateTime_EmptyString_ReturnsMinValue()
		{
			Sql.ToDateTime("").Should().Be(DateTime.MinValue);
		}

		[Fact]
		public void ToDateTime_ValidDateString_ReturnsDate()
		{
			DateTime result = Sql.ToDateTime("2025-01-15");
			result.Year.Should().Be(2025);
			result.Month.Should().Be(1);
			result.Day.Should().Be(15);
		}

		[Fact]
		public void ToDateTime_Object_Null_ReturnsMinValue()
		{
			Sql.ToDateTime((object?)null).Should().Be(DateTime.MinValue);
		}

		[Fact]
		public void ToDateTime_Object_DBNull_ReturnsMinValue()
		{
			Sql.ToDateTime(DBNull.Value).Should().Be(DateTime.MinValue);
		}

		// =====================================================================
		// ToString
		// =====================================================================
		[Fact]
		public void ToString_NullString_ReturnsEmpty()
		{
			Sql.ToString((string?)null).Should().BeEmpty();
		}

		[Fact]
		public void ToString_NullObject_ReturnsEmpty()
		{
			Sql.ToString((object?)null).Should().BeEmpty();
		}

		[Fact]
		public void ToString_DBNull_ReturnsEmpty()
		{
			Sql.ToString(DBNull.Value).Should().BeEmpty();
		}

		[Fact]
		public void ToString_ValidObject_ReturnsString()
		{
			Sql.ToString((object)"hello").Should().Be("hello");
		}

		[Fact]
		public void ToString_IntObject_ReturnsIntString()
		{
			Sql.ToString((object)42).Should().Be("42");
		}

		// =====================================================================
		// ToBinary
		// =====================================================================
		[Fact]
		public void ToBinary_NullObject_ReturnsEmptyByteArray()
		{
			byte[] result = Sql.ToBinary(null);
			result.Should().NotBeNull();
			result.Should().BeEmpty();
		}

		[Fact]
		public void ToBinary_DBNull_ReturnsEmptyByteArray()
		{
			byte[] result = Sql.ToBinary(DBNull.Value);
			result.Should().NotBeNull();
			result.Should().BeEmpty();
		}

		[Fact]
		public void ToBinary_ValidByteArray_ReturnsSameArray()
		{
			byte[] input = new byte[] { 1, 2, 3, 4 };
			byte[] result = Sql.ToBinary(input);
			result.Should().BeEquivalentTo(input);
		}

		// =====================================================================
		// ToDBString
		// =====================================================================
		[Fact]
		public void ToDBString_Null_ReturnsDBNull()
		{
			Sql.ToDBString((string?)null).Should().Be(DBNull.Value);
		}

		[Fact]
		public void ToDBString_EmptyString_ReturnsDBNull()
		{
			Sql.ToDBString("").Should().Be(DBNull.Value);
		}

		[Fact]
		public void ToDBString_ValidString_ReturnsString()
		{
			Sql.ToDBString("test").Should().Be("test");
		}

		// =====================================================================
		// ToFloat / ToDouble
		// =====================================================================
		[Fact]
		public void ToDouble_Null_ReturnsZero()
		{
			Sql.ToDouble((object?)null).Should().Be(0.0);
		}

		[Fact]
		public void ToDouble_ValidString_ReturnsValue()
		{
			Sql.ToDouble("3.14").Should().BeApproximately(3.14, 0.001);
		}
	}
}
