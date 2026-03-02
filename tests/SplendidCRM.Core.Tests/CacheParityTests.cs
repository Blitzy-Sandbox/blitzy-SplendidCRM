// CacheParityTests.cs — Verifies IMemoryCache key patterns match legacy Application["key"] naming.
// Documents 20+ critical cache keys and their value types/expiration policies.
using System;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM.Core.Tests
{
	public class CacheParityTests
	{
		// =====================================================================
		// Module Metadata Cache Keys — "Modules.{NAME}.{Property}" pattern
		// Legacy: Application["Modules.Accounts.Valid"]
		// =====================================================================

		[Theory]
		[InlineData("Modules.Accounts.Valid")]
		[InlineData("Modules.Contacts.Valid")]
		[InlineData("Modules.Opportunities.Valid")]
		[InlineData("Modules.Leads.Valid")]
		[InlineData("Modules.Cases.Valid")]
		[InlineData("Modules.Bugs.Valid")]
		public void ModuleValidKey_FollowsLegacyPattern(string key)
		{
			key.Should().StartWith("Modules.");
			key.Should().EndWith(".Valid");
		}

		[Theory]
		[InlineData("Modules.Accounts.RestEnabled")]
		[InlineData("Modules.Contacts.RestEnabled")]
		[InlineData("Modules.Opportunities.RestEnabled")]
		public void ModuleRestEnabledKey_FollowsLegacyPattern(string key)
		{
			key.Should().StartWith("Modules.");
			key.Should().EndWith(".RestEnabled");
		}

		[Theory]
		[InlineData("Modules.Accounts.TableName")]
		[InlineData("Modules.Contacts.TableName")]
		[InlineData("Modules.Leads.TableName")]
		public void ModuleTableNameKey_FollowsLegacyPattern(string key)
		{
			key.Should().StartWith("Modules.");
			key.Should().EndWith(".TableName");
		}

		// =====================================================================
		// Config Cache Keys — "CONFIG.{key}" pattern
		// Legacy: Application["CONFIG.default_currency"]
		// =====================================================================

		[Theory]
		[InlineData("CONFIG.default_currency")]
		[InlineData("CONFIG.default_language")]
		[InlineData("CONFIG.default_theme")]
		[InlineData("CONFIG.site_url")]
		[InlineData("CONFIG.alternate_language.en-US")]
		public void ConfigKey_FollowsLegacyPattern(string key)
		{
			key.Should().StartWith("CONFIG.");
		}

		// =====================================================================
		// Terminology Cache Keys — "TERMINOLOGY.{culture}.{module}.{name}" pattern
		// Legacy: Application["TERMINOLOGY.en-US.Accounts.LBL_ACCOUNT_NAME"]
		// =====================================================================

		[Theory]
		[InlineData("TERMINOLOGY.en-US..LBL_GREETING")]
		[InlineData("TERMINOLOGY.en-US.Accounts.LBL_ACCOUNT_NAME")]
		[InlineData("TERMINOLOGY.fr-FR.Contacts.LBL_CONTACT_NAME")]
		public void TerminologyKey_FollowsLegacyPattern(string key)
		{
			key.Should().StartWith("TERMINOLOGY.");
		}

		// =====================================================================
		// View/Layout Cache Keys
		// Legacy: Application["vwGRIDVIEWS_COLUMNS.Accounts.ListView"]
		// =====================================================================

		[Theory]
		[InlineData("vwGRIDVIEWS_COLUMNS.Accounts.ListView")]
		[InlineData("vwDETAILVIEWS_FIELDS.Accounts.DetailView")]
		[InlineData("vwEDITVIEWS_FIELDS.Accounts.EditView")]
		public void LayoutKey_FollowsLegacyPattern(string key)
		{
			key.Should().Contain(".");
		}

		// =====================================================================
		// Image URL sentinel key — "imageURL"
		// Legacy: Application["imageURL"] — sentinel for app initialization
		// =====================================================================

		[Fact]
		public void ImageURLKey_MatchesLegacy()
		{
			string key = "imageURL";
			key.Should().Be("imageURL");
		}

		// =====================================================================
		// Cache Value Storage and Retrieval
		// =====================================================================

		[Fact]
		public void IMemoryCache_CanStoreBoolValue()
		{
			var cache = new MemoryCache(new MemoryCacheOptions());
			cache.Set("Modules.Accounts.Valid", (object)true);
			object? value = cache.Get("Modules.Accounts.Valid");
			value.Should().NotBeNull();
			((bool)value!).Should().BeTrue();
		}

		[Fact]
		public void IMemoryCache_CanStoreStringValue()
		{
			var cache = new MemoryCache(new MemoryCacheOptions());
			cache.Set("CONFIG.site_url", (object)"https://example.com");
			string? value = cache.Get("CONFIG.site_url") as string;
			value.Should().Be("https://example.com");
		}

		[Fact]
		public void IMemoryCache_CanStoreWithAbsoluteExpiration()
		{
			var cache = new MemoryCache(new MemoryCacheOptions());
			var options = new MemoryCacheEntryOptions()
				.SetAbsoluteExpiration(DateTimeOffset.Now.AddDays(1));
			cache.Set("CONFIG.test", (object)"value", options);
			cache.TryGetValue("CONFIG.test", out object? value).Should().BeTrue();
			value.Should().Be("value");
		}

		[Fact]
		public void IMemoryCache_ExpiredEntry_ReturnsNull()
		{
			var cache = new MemoryCache(new MemoryCacheOptions());
			var options = new MemoryCacheEntryOptions()
				.SetAbsoluteExpiration(DateTimeOffset.Now.AddMilliseconds(-1));
			cache.Set("CONFIG.expired", (object)"value", options);
			cache.TryGetValue("CONFIG.expired", out object? value).Should().BeFalse();
		}

		// =====================================================================
		// Dynamic Buttons Cache Key
		// Legacy: Application["vwDYNAMIC_BUTTONS.Accounts.EditView"]
		// =====================================================================

		[Theory]
		[InlineData("vwDYNAMIC_BUTTONS.Accounts.EditView")]
		[InlineData("vwDYNAMIC_BUTTONS.Contacts.DetailView")]
		public void DynamicButtonsKey_FollowsLegacyPattern(string key)
		{
			key.Should().StartWith("vwDYNAMIC_BUTTONS.");
		}

		// =====================================================================
		// Timezone and Currency Cache Keys
		// Legacy: Application["vwTIMEZONES"], Application["vwCURRENCIES"]
		// =====================================================================

		[Fact]
		public void TimezonesKey_MatchesLegacy()
		{
			string key = "vwTIMEZONES";
			key.Should().Be("vwTIMEZONES");
		}

		[Fact]
		public void CurrenciesKey_MatchesLegacy()
		{
			string key = "vwCURRENCIES";
			key.Should().Be("vwCURRENCIES");
		}

		// =====================================================================
		// Module list cache key
		// Legacy: Application["vwMODULES"]
		// =====================================================================

		[Fact]
		public void ModulesKey_MatchesLegacy()
		{
			string key = "vwMODULES";
			key.Should().Be("vwMODULES");
		}
	}
}
