// SearchBuilderTests.cs — Unit tests for SplendidCRM.SearchBuilder.
// Tests constructor tokenization and query building using mock IDbCommand.
using System;
using System.Data;
using System.Data.Common;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class SearchBuilderTests
	{
		private IDbCommand CreateMockCommand()
		{
			var mockCmd = new Mock<IDbCommand>();
			var parameters = new DataParameterCollection();
			mockCmd.Setup(c => c.Parameters).Returns(parameters);
			mockCmd.SetupProperty(c => c.CommandText, "SELECT * FROM Accounts WHERE 1=1");
			mockCmd.Setup(c => c.CreateParameter()).Returns(() =>
			{
				var p = new Mock<IDbDataParameter>();
				p.SetupAllProperties();
				return p.Object;
			});
			return mockCmd.Object;
		}

		// Simple IDataParameterCollection mock
		private class DataParameterCollection : System.Collections.ArrayList, IDataParameterCollection
		{
			public bool Contains(string parameterName) => false;
			public int IndexOf(string parameterName) => -1;
			public void RemoveAt(string parameterName) { }
			public object this[string parameterName]
			{
				get => throw new NotImplementedException();
				set => throw new NotImplementedException();
			}
		}

		// =====================================================================
		// Constructor — does not throw on valid inputs
		// =====================================================================

		[Fact]
		public void Constructor_WithSimpleSearch_DoesNotThrow()
		{
			var cmd = CreateMockCommand();

			// SearchBuilder(string str, IDbCommand cmd) — tokenizes search string
			Action act = () => new SearchBuilder("John Smith", cmd);
			act.Should().NotThrow();
		}

		[Fact]
		public void Constructor_EmptyString_DoesNotThrow()
		{
			var cmd = CreateMockCommand();
			Action act = () => new SearchBuilder("", cmd);
			act.Should().NotThrow();
		}

		// =====================================================================
		// Static OData-related parsing tests via RestUtil.ConvertODataFilter
		// =====================================================================

		[Fact]
		public void ConvertODataFilter_EmptyFilter_DoesNotModifyCommand()
		{
			var cmd = CreateMockCommand();
			string originalText = cmd.CommandText;
			RestUtil.ConvertODataFilter("", cmd);
			cmd.CommandText.Should().Be(originalText);
		}

		[Fact]
		public void ConvertODataFilter_NullFilter_DoesNotModifyCommand()
		{
			var cmd = CreateMockCommand();
			string originalText = cmd.CommandText;
			RestUtil.ConvertODataFilter(null, cmd);
			cmd.CommandText.Should().Be(originalText);
		}

		[Fact]
		public void ConvertODataFilter_EqOperator_AppendsEqualityClause()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("NAME eq 'Test'", cmd);
			cmd.CommandText.Should().Contain("NAME = @NAME");
			cmd.Parameters.Count.Should().BeGreaterThan(0);
		}

		[Fact]
		public void ConvertODataFilter_NeOperator_AppendsNotEqual()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("STATUS ne 'Closed'", cmd);
			cmd.CommandText.Should().Contain("STATUS <> @STATUS");
		}

		[Fact]
		public void ConvertODataFilter_GeOperator_AppendsGreaterOrEqual()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("VALUE ge 5", cmd);
			cmd.CommandText.Should().Contain("VALUE >= @VALUE");
		}

		[Fact]
		public void ConvertODataFilter_ContainsFunction_AppendsLikeClause()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("contains(NAME, 'test')", cmd);
			cmd.CommandText.Should().Contain("NAME like");
		}

		[Fact]
		public void ConvertODataFilter_StartsWithFunction_AppendsLikeClause()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("startswith(NAME, 'Acme')", cmd);
			cmd.CommandText.Should().Contain("NAME like");
		}

		[Fact]
		public void ConvertODataFilter_MultipleAndConditions_AppendsAll()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("NAME eq 'Test' and STATUS eq 'Active'", cmd);
			cmd.CommandText.Should().Contain("NAME =");
			cmd.CommandText.Should().Contain("STATUS =");
			cmd.Parameters.Count.Should().Be(2);
		}

		[Fact]
		public void ConvertODataFilter_GtOperator_AppendsGreaterThan()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("AMOUNT gt 100", cmd);
			cmd.CommandText.Should().Contain("AMOUNT > @AMOUNT");
		}

		[Fact]
		public void ConvertODataFilter_LtOperator_AppendsLessThan()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("AMOUNT lt 50", cmd);
			cmd.CommandText.Should().Contain("AMOUNT < @AMOUNT");
		}

		[Fact]
		public void ConvertODataFilter_LeOperator_AppendsLessOrEqual()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("COUNT le 10", cmd);
			cmd.CommandText.Should().Contain("COUNT <= @COUNT");
		}

		[Fact]
		public void ConvertODataFilter_NullValue_HandledCorrectly()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("DELETED eq null", cmd);
			cmd.CommandText.Should().Contain("DELETED");
		}

		[Fact]
		public void ConvertODataFilter_BooleanTrueValue_Handled()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("ACTIVE eq true", cmd);
			cmd.CommandText.Should().Contain("ACTIVE =");
		}

		[Fact]
		public void ConvertODataFilter_EndsWith_AppendsLikeClause()
		{
			var cmd = CreateMockCommand();
			RestUtil.ConvertODataFilter("endswith(EMAIL, '.com')", cmd);
			cmd.CommandText.Should().Contain("EMAIL like");
		}
	}
}
