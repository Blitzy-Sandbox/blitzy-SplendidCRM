// RestUtilTests.cs — Unit tests for SplendidCRM.RestUtil static/pure methods.
using System;
using System.Data;
using Xunit;
using FluentAssertions;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class RestUtilTests
	{
		// =====================================================================
		// UnixTicks
		// =====================================================================

		[Fact]
		public void UnixTicks_Epoch_ReturnsZero()
		{
			DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			long result = RestUtil.UnixTicks(epoch);
			result.Should().Be(0);
		}

		[Fact]
		public void UnixTicks_KnownDate_ReturnsExpectedMilliseconds()
		{
			// 2025-01-01T00:00:00Z = 1735689600000 ms since epoch
			DateTime dt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			long result = RestUtil.UnixTicks(dt);
			result.Should().Be(1735689600000L);
		}

		[Fact]
		public void UnixTicks_MinValue_ReturnsNegativeOrZero()
		{
			// DateTime.MinValue is way before epoch, will be negative
			long result = RestUtil.UnixTicks(DateTime.MinValue);
			result.Should().BeLessThan(0);
		}

		// =====================================================================
		// ToJsonDate
		// =====================================================================

		[Fact]
		public void ToJsonDate_Null_ReturnsNull()
		{
			string? result = RestUtil.ToJsonDate(null);
			result.Should().BeNull();
		}

		[Fact]
		public void ToJsonDate_DBNull_ReturnsNull()
		{
			string? result = RestUtil.ToJsonDate(DBNull.Value);
			result.Should().BeNull();
		}

		[Fact]
		public void ToJsonDate_ValidDateTime_ReturnsDatePattern()
		{
			DateTime dt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
			string? result = RestUtil.ToJsonDate(dt);
			result.Should().StartWith("/Date(");
			result.Should().EndWith(")/");
		}

		[Fact]
		public void ToJsonDate_MinValue_ReturnsNull()
		{
			string? result = RestUtil.ToJsonDate(DateTime.MinValue);
			result.Should().BeNull();
		}

		[Fact]
		public void ToJsonDate_KnownDate_ContainsCorrectTicks()
		{
			DateTime dt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			string? result = RestUtil.ToJsonDate(dt);
			result.Should().Contain("1735689600000");
		}

		// =====================================================================
		// FromJsonDate
		// =====================================================================

		[Fact]
		public void FromJsonDate_Null_ReturnsMinValue()
		{
			DateTime result = RestUtil.FromJsonDate(null);
			result.Should().Be(DateTime.MinValue);
		}

		[Fact]
		public void FromJsonDate_EmptyString_ReturnsMinValue()
		{
			DateTime result = RestUtil.FromJsonDate("");
			result.Should().Be(DateTime.MinValue);
		}

		[Fact]
		public void FromJsonDate_DateTicksFormat_ReturnsCorrectDate()
		{
			// /Date(1735689600000)/ = 2025-01-01T00:00:00Z
			DateTime result = RestUtil.FromJsonDate("/Date(1735689600000)/");
			result.Year.Should().Be(2025);
			result.Month.Should().Be(1);
			result.Day.Should().Be(1);
		}

		[Fact]
		public void FromJsonDate_DateTicksWithTimezone_StripsOffset()
		{
			DateTime result = RestUtil.FromJsonDate("/Date(1735689600000+0000)/");
			result.Year.Should().Be(2025);
		}

		[Fact]
		public void FromJsonDate_ISOFormat_ParsesDate()
		{
			DateTime result = RestUtil.FromJsonDate("2025-06-15T12:00:00");
			result.Year.Should().Be(2025);
			result.Month.Should().Be(6);
			result.Day.Should().Be(15);
		}

		// =====================================================================
		// RoundTrip: ToJsonDate → FromJsonDate
		// =====================================================================

		[Fact]
		public void ToJsonDate_FromJsonDate_RoundTrip_PreservesDate()
		{
			DateTime original = new DateTime(2025, 3, 15, 10, 30, 0, DateTimeKind.Utc);
			string? json = RestUtil.ToJsonDate(original);
			json.Should().NotBeNull();
			DateTime recovered = RestUtil.FromJsonDate(json!);
			recovered.Year.Should().Be(original.Year);
			recovered.Month.Should().Be(original.Month);
			recovered.Day.Should().Be(original.Day);
			recovered.Hour.Should().Be(original.Hour);
			recovered.Minute.Should().Be(original.Minute);
		}
	}
}
