// UtilsTests.cs — Unit tests for SplendidCRM.Utils static methods.
using System;
using System.Data;
using Xunit;
using FluentAssertions;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class UtilsTests
	{
		// =====================================================================
		// ExpandException
		// =====================================================================

		[Fact]
		public void ExpandException_SimpleException_ReturnsMessage()
		{
			var ex = new Exception("Test error");
			string result = Utils.ExpandException(ex);
			result.Should().Contain("Test error");
		}

		[Fact]
		public void ExpandException_NestedException_ReturnsBothMessages()
		{
			var inner = new Exception("Inner error");
			var outer = new Exception("Outer error", inner);
			string result = Utils.ExpandException(outer);
			result.Should().Contain("Outer error");
			result.Should().Contain("Inner error");
		}

		[Fact]
		public void ExpandException_DeepNesting_ReturnsAllMessages()
		{
			var level3 = new Exception("Level 3");
			var level2 = new Exception("Level 2", level3);
			var level1 = new Exception("Level 1", level2);
			string result = Utils.ExpandException(level1);
			result.Should().Contain("Level 1");
			result.Should().Contain("Level 2");
			result.Should().Contain("Level 3");
		}

		// =====================================================================
		// NormalizePhone
		// =====================================================================

		[Theory]
		[InlineData("(555) 123-4567", "5551234567")]
		[InlineData("+1-555-123-4567", "15551234567")]
		[InlineData("555.123.4567", "5551234567")]
		[InlineData("555 123 4567", "5551234567")]
		[InlineData("", "")]
		public void NormalizePhone_ShouldStripSpecialChars(string input, string expected)
		{
			string result = Utils.NormalizePhone(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void NormalizePhone_Null_ReturnsEmpty()
		{
			string result = Utils.NormalizePhone(null);
			result.Should().BeEmpty();
		}

		[Fact]
		public void NormalizePhone_BracketsAndHash_Removed()
		{
			string result = Utils.NormalizePhone("[555]#123*456%789");
			result.Should().Be("555123456789");
		}

		// =====================================================================
		// ContentDispositionEncode
		// =====================================================================

		[Fact]
		public void ContentDispositionEncode_AsciiFilename_WrapsInQuotes()
		{
			string result = Utils.ContentDispositionEncode("report.pdf");
			result.Should().Be("\"report.pdf\"");
		}

		[Fact]
		public void ContentDispositionEncode_Null_ThrowsNullReference()
		{
			// ContentDispositionEncode does not handle null — calls Replace on null string
			Action act = () => Utils.ContentDispositionEncode((string?)null!);
			act.Should().Throw<NullReferenceException>();
		}

		[Fact]
		public void ContentDispositionEncode_EmptyString_ReturnsQuotedEmpty()
		{
			string result = Utils.ContentDispositionEncode("");
			result.Should().Be("\"\"");
		}

		[Fact]
		public void ContentDispositionEncode_BackslashReplaced()
		{
			string result = Utils.ContentDispositionEncode(@"path\to\file.pdf");
			result.Should().NotContain("\\");
			result.Should().Contain("_");
		}

		[Fact]
		public void ContentDispositionEncode_ColonReplaced()
		{
			string result = Utils.ContentDispositionEncode("C:file.pdf");
			result.Should().NotContain(":");
			result.Should().Contain("_");
		}

		// =====================================================================
		// GenerateVCard
		// =====================================================================

		[Fact]
		public void GenerateVCard_WithNameFields_ContainsVCardStructure()
		{
			DataTable dt = new DataTable();
			dt.Columns.Add("ID", typeof(Guid));
			dt.Columns.Add("SALUTATION", typeof(string));
			dt.Columns.Add("NAME", typeof(string));
			dt.Columns.Add("FIRST_NAME", typeof(string));
			dt.Columns.Add("LAST_NAME", typeof(string));
			dt.Columns.Add("TITLE", typeof(string));
			dt.Columns.Add("PHONE_HOME", typeof(string));
			dt.Columns.Add("PHONE_MOBILE", typeof(string));
			dt.Columns.Add("PHONE_WORK", typeof(string));
			dt.Columns.Add("PHONE_OTHER", typeof(string));
			dt.Columns.Add("PHONE_FAX", typeof(string));
			dt.Columns.Add("EMAIL1", typeof(string));
			dt.Columns.Add("EMAIL2", typeof(string));
			dt.Columns.Add("ACCOUNT_NAME", typeof(string));
			dt.Columns.Add("DEPARTMENT", typeof(string));
			dt.Columns.Add("ASSISTANT", typeof(string));
			dt.Columns.Add("ASSISTANT_PHONE", typeof(string));
			dt.Columns.Add("PRIMARY_ADDRESS_STREET", typeof(string));
			dt.Columns.Add("PRIMARY_ADDRESS_CITY", typeof(string));
			dt.Columns.Add("PRIMARY_ADDRESS_STATE", typeof(string));
			dt.Columns.Add("PRIMARY_ADDRESS_POSTALCODE", typeof(string));
			dt.Columns.Add("PRIMARY_ADDRESS_COUNTRY", typeof(string));
			dt.Columns.Add("ALT_ADDRESS_STREET", typeof(string));
			dt.Columns.Add("ALT_ADDRESS_CITY", typeof(string));
			dt.Columns.Add("ALT_ADDRESS_STATE", typeof(string));
			dt.Columns.Add("ALT_ADDRESS_POSTALCODE", typeof(string));
			dt.Columns.Add("ALT_ADDRESS_COUNTRY", typeof(string));
			dt.Columns.Add("DESCRIPTION", typeof(string));
			dt.Columns.Add("BIRTHDATE", typeof(DateTime));
			dt.Columns.Add("DATE_MODIFIED_UTC", typeof(DateTime));
			dt.Columns.Add("PICTURE", typeof(string));

			DataRow row = dt.NewRow();
			row["ID"] = Guid.NewGuid();
			row["SALUTATION"] = "Mr.";
			row["NAME"] = "John Doe";
			row["FIRST_NAME"] = "John";
			row["LAST_NAME"] = "Doe";
			row["TITLE"] = "Engineer";
			row["EMAIL1"] = "john@example.com";
			row["ACCOUNT_NAME"] = "Acme Corp";
			row["PHONE_WORK"] = "555-1234";
			row["PHONE_HOME"] = "";
			row["PHONE_MOBILE"] = "";
			row["PHONE_OTHER"] = "";
			row["PHONE_FAX"] = "";
			row["EMAIL2"] = "";
			row["DEPARTMENT"] = "";
			row["ASSISTANT"] = "";
			row["ASSISTANT_PHONE"] = "";
			row["PRIMARY_ADDRESS_STREET"] = "123 Main St";
			row["PRIMARY_ADDRESS_CITY"] = "Anytown";
			row["PRIMARY_ADDRESS_STATE"] = "CA";
			row["PRIMARY_ADDRESS_POSTALCODE"] = "90210";
			row["PRIMARY_ADDRESS_COUNTRY"] = "US";
			row["ALT_ADDRESS_STREET"] = "";
			row["ALT_ADDRESS_CITY"] = "";
			row["ALT_ADDRESS_STATE"] = "";
			row["ALT_ADDRESS_POSTALCODE"] = "";
			row["ALT_ADDRESS_COUNTRY"] = "";
			row["DESCRIPTION"] = "";
			row["PICTURE"] = "";
			dt.Rows.Add(row);

			string result = Utils.GenerateVCard(row);
			result.Should().Contain("BEGIN:VCARD");
			result.Should().Contain("END:VCARD");
			result.Should().Contain("Doe");
			result.Should().Contain("John");
		}

		[Fact]
		public void GenerateVCard_WithEmail_ContainsEmailField()
		{
			DataTable dt = CreateMinimalContactTable();
			DataRow row = dt.NewRow();
			PopulateMinimalRow(row);
			row["EMAIL1"] = "test@example.com";
			dt.Rows.Add(row);

			string result = Utils.GenerateVCard(row);
			result.Should().Contain("test@example.com");
		}

		[Fact]
		public void GenerateVCard_VersionPresent()
		{
			DataTable dt = CreateMinimalContactTable();
			DataRow row = dt.NewRow();
			PopulateMinimalRow(row);
			dt.Rows.Add(row);

			string result = Utils.GenerateVCard(row);
			result.Should().Contain("VERSION:3.0");
		}

		// Helper to create minimal contact DataTable with all required columns
		private DataTable CreateMinimalContactTable()
		{
			DataTable dt = new DataTable();
			foreach (string col in new[] { "ID", "SALUTATION", "NAME", "FIRST_NAME", "LAST_NAME", "TITLE",
				"PHONE_HOME", "PHONE_MOBILE", "PHONE_WORK", "PHONE_OTHER", "PHONE_FAX",
				"EMAIL1", "EMAIL2", "ACCOUNT_NAME", "DEPARTMENT", "ASSISTANT", "ASSISTANT_PHONE",
				"PRIMARY_ADDRESS_STREET", "PRIMARY_ADDRESS_CITY", "PRIMARY_ADDRESS_STATE",
				"PRIMARY_ADDRESS_POSTALCODE", "PRIMARY_ADDRESS_COUNTRY",
				"ALT_ADDRESS_STREET", "ALT_ADDRESS_CITY", "ALT_ADDRESS_STATE",
				"ALT_ADDRESS_POSTALCODE", "ALT_ADDRESS_COUNTRY", "DESCRIPTION", "PICTURE" })
			{
				dt.Columns.Add(col, typeof(string));
			}
			dt.Columns.Add("BIRTHDATE", typeof(DateTime));
			dt.Columns.Add("DATE_MODIFIED_UTC", typeof(DateTime));
			dt.Columns["ID"]!.DataType = typeof(Guid);
			return dt;
		}

		private void PopulateMinimalRow(DataRow row)
		{
			row["ID"] = Guid.NewGuid();
			foreach (DataColumn col in row.Table.Columns)
			{
				if (row[col] == DBNull.Value && col.DataType == typeof(string))
					row[col] = "";
			}
			row["FIRST_NAME"] = "Jane";
			row["LAST_NAME"] = "Smith";
		}
	}
}
