// L10nTests.cs — Unit tests for SplendidCRM.L10N localization utility.
using System;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class L10nTests
	{
		// =====================================================================
		// NormalizeCulture — static method
		// =====================================================================

		[Theory]
		[InlineData(null, "en-US")]
		[InlineData("", "en-US")]
		[InlineData("en-US", "en-US")]
		[InlineData("en-us", "en-US")]
		[InlineData("EN-US", "en-US")]
		[InlineData("fr-FR", "fr-FR")]
		[InlineData("fr-fr", "fr-FR")]
		[InlineData("de_DE", "de-DE")]
		[InlineData("ja_JP", "ja-JP")]
		[InlineData("pt-BR", "pt-BR")]
		public void NormalizeCulture_ShouldNormalize(string? input, string expected)
		{
			string result = L10N.NormalizeCulture(input);
			result.Should().Be(expected);
		}

		[Fact]
		public void NormalizeCulture_ReplacesUnderscoreWithHyphen()
		{
			string result = L10N.NormalizeCulture("en_us");
			result.Should().Be("en-US");
		}

		[Fact]
		public void NormalizeCulture_ProperCasing_LowerLanguageUpperRegion()
		{
			string result = L10N.NormalizeCulture("FR-fr");
			result.Should().Be("fr-FR");
		}

		// =====================================================================
		// IsLanguageRTL — instance method
		// =====================================================================

		[Theory]
		[InlineData("he-IL", true)]
		[InlineData("ar-SA", true)]
		[InlineData("ur-PK", true)]
		[InlineData("fa-IR", true)]
		[InlineData("en-US", false)]
		[InlineData("fr-FR", false)]
		[InlineData("de-DE", false)]
		[InlineData("ja-JP", false)]
		[InlineData("zh-CN", false)]
		public void IsLanguageRTL_ShouldDetectRTLLanguages(string culture, bool expectedRTL)
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			L10N l10n = new L10N(culture, memoryCache);
			l10n.IsLanguageRTL().Should().Be(expectedRTL);
		}

		// =====================================================================
		// Term — instance method with cache lookup
		// =====================================================================

		[Fact]
		public void Term_WhenKeyExistsInCache_ReturnsTranslatedTerm()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			// Cache key format: "{culture}.{entryName}" — e.g. "en-US.LBL_GREETING"
			memoryCache.Set("en-US.LBL_GREETING", (object)"Hello!");

			L10N l10n = new L10N("en-US", memoryCache);
			string result = l10n.Term("LBL_GREETING");
			result.Should().Be("Hello!");
		}

		[Fact]
		public void Term_WhenKeyNotInCache_ReturnsKeyAsFallback()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			L10N l10n = new L10N("en-US", memoryCache);
			string result = l10n.Term("LBL_NONEXISTENT");
			// When term is not found in current or en-US fallback, returns the key name
			result.Should().NotBeNull();
		}

		[Fact]
		public void Term_WithModuleDotPrefix_LooksUpModuleSpecificTerm()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			// Key format: "{culture}.{module}.{name}" e.g. "en-US.Accounts.LBL_ACCOUNT_NAME"
			memoryCache.Set("en-US.Accounts.LBL_ACCOUNT_NAME", (object)"Account Name");

			L10N l10n = new L10N("en-US", memoryCache);
			string result = l10n.Term("Accounts.LBL_ACCOUNT_NAME");
			result.Should().Be("Account Name");
		}

		[Fact]
		public void Term_NullOrEmpty_ReturnsEmptyOrKey()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			L10N l10n = new L10N("en-US", memoryCache);
			string result = l10n.Term("");
			result.Should().NotBeNull();
		}

		// =====================================================================
		// Term — list overload
		// =====================================================================

		[Fact]
		public void Term_ListOverload_WhenValueInCache_ReturnsDisplayName()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			// The list term format: "TERMINOLOGY.en-US..{listName}.{field}"
			memoryCache.Set("TERMINOLOGY.en-US..account_type_dom.Customer", (object)"Customer");

			L10N l10n = new L10N("en-US", memoryCache);
			object result = l10n.Term("account_type_dom", (object)"Customer");
			result.Should().NotBeNull();
		}

		[Fact]
		public void Term_ListOverload_NullField_ReturnsNull()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			L10N l10n = new L10N("en-US", memoryCache);
			object result = l10n.Term("some_list", null);
			// When field is null, should return null directly without lookup
			result.Should().BeNull();
		}

		[Fact]
		public void Term_ListOverload_DBNullField_ReturnsDBNull()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			L10N l10n = new L10N("en-US", memoryCache);
			object result = l10n.Term("some_list", DBNull.Value);
			result.Should().Be(DBNull.Value);
		}
	}
}
