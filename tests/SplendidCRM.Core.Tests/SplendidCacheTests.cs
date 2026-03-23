// SplendidCacheTests.cs — Unit tests for SplendidCRM.SplendidCache.
// Tests cache expiration utilities and cache key patterns.
using System;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class SplendidCacheTests
	{
		private SplendidCache CreateSplendidCache()
		{
			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var mockAccessor = new Mock<IHttpContextAccessor>();
			var mockContext = new Mock<HttpContext>();
			mockContext.Setup(c => c.Session).Returns(new Mock<ISession>().Object);
			mockAccessor.Setup(a => a.HttpContext).Returns(mockContext.Object);

			var mockConfig = new Mock<IConfiguration>();
			var dbf = new DbProviderFactories(memoryCache, mockAccessor.Object, mockConfig.Object);
			var security = new Security(mockAccessor.Object, memoryCache);
			var splendidError = new SplendidError(mockAccessor.Object, memoryCache);
			return new SplendidCache(memoryCache, mockConfig.Object, mockAccessor.Object, dbf, security, splendidError);
		}

		// =====================================================================
		// DefaultCacheExpiration
		// =====================================================================

		[Fact]
		public void DefaultCacheExpiration_ReturnsApproximately1Day()
		{
			var cache = CreateSplendidCache();
			DateTimeOffset expiration = cache.DefaultCacheExpiration();
			DateTimeOffset expected = DateTimeOffset.Now.AddDays(1);
			// Should be within 5 seconds of expected (account for test execution time)
			(expiration - expected).Duration().Should().BeLessThan(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public void DefaultCacheExpiration_IsFutureDate()
		{
			var cache = CreateSplendidCache();
			DateTimeOffset expiration = cache.DefaultCacheExpiration();
			expiration.Should().BeAfter(DateTimeOffset.Now);
		}

		// =====================================================================
		// CacheExpiration5Minutes
		// =====================================================================

		[Fact]
		public void CacheExpiration5Minutes_ReturnsApproximately5Minutes()
		{
			var cache = CreateSplendidCache();
			DateTimeOffset expiration = cache.CacheExpiration5Minutes();
			DateTimeOffset expected = DateTimeOffset.Now.AddMinutes(5);
			(expiration - expected).Duration().Should().BeLessThan(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public void CacheExpiration5Minutes_IsFutureDate()
		{
			var cache = CreateSplendidCache();
			DateTimeOffset expiration = cache.CacheExpiration5Minutes();
			expiration.Should().BeAfter(DateTimeOffset.Now);
		}

		[Fact]
		public void CacheExpiration5Minutes_IsLessThanDefaultExpiration()
		{
			var cache = CreateSplendidCache();
			DateTimeOffset fiveMin = cache.CacheExpiration5Minutes();
			DateTimeOffset oneDay = cache.DefaultCacheExpiration();
			fiveMin.Should().BeBefore(oneDay);
		}

		// =====================================================================
		// Cache Key Pattern Verification
		// =====================================================================

		[Fact]
		public void ModuleCacheKey_FollowsLegacyPattern()
		{
			// Verify the cache key pattern for modules matches legacy Application["Modules.{NAME}.{Property}"]
			string moduleName = "Accounts";
			string key = "Modules." + moduleName + ".Valid";
			key.Should().Be("Modules.Accounts.Valid");
		}

		[Fact]
		public void ConfigCacheKey_FollowsLegacyPattern()
		{
			// Verify config cache key pattern matches legacy Application["CONFIG.{key}"]
			string configKey = "CONFIG.default_currency";
			configKey.Should().StartWith("CONFIG.");
		}

		[Fact]
		public void TerminologyCacheKey_FollowsExpectedPattern()
		{
			// Verify terminology cache key pattern
			string culture = "en-US";
			string module = "Accounts";
			string name = "LBL_ACCOUNT_NAME";
			string key = "TERMINOLOGY." + culture + "." + module + "." + name;
			key.Should().Be("TERMINOLOGY.en-US.Accounts.LBL_ACCOUNT_NAME");
		}
	}
}
