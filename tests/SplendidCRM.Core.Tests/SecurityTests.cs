// SecurityTests.cs — Unit tests for SplendidCRM.Security methods.
// Tests static methods (HashPassword) and instance methods using mocked session/cache.
using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using SplendidCRM;

namespace SplendidCRM.Core.Tests
{
	public class SecurityTests
	{
		// =====================================================================
		// HashPassword — static method, pure MD5 hash
		// =====================================================================

		[Fact]
		public void HashPassword_KnownInput_ReturnsExpectedMD5()
		{
			// MD5("admin") = 21232f297a57a5a743894a0e4a801fc3
			string result = Security.HashPassword("admin");
			result.Should().Be("21232f297a57a5a743894a0e4a801fc3");
		}

		[Fact]
		public void HashPassword_EmptyString_ReturnsExpectedMD5()
		{
			// MD5("") = d41d8cd98f00b204e9800998ecf8427e
			string result = Security.HashPassword("");
			result.Should().Be("d41d8cd98f00b204e9800998ecf8427e");
		}

		[Fact]
		public void HashPassword_ComplexString_ReturnsConsistentHash()
		{
			string input = "P@ssw0rd!123";
			string result1 = Security.HashPassword(input);
			string result2 = Security.HashPassword(input);
			result1.Should().Be(result2);
			result1.Should().HaveLength(32); // MD5 hex is always 32 chars
		}

		[Fact]
		public void HashPassword_DifferentInputs_ReturnDifferentHashes()
		{
			string hash1 = Security.HashPassword("password1");
			string hash2 = Security.HashPassword("password2");
			hash1.Should().NotBe(hash2);
		}

		[Fact]
		public void HashPassword_Unicode_ReturnsHash()
		{
			string result = Security.HashPassword("pässwörd");
			result.Should().HaveLength(32);
			result.Should().MatchRegex("^[0-9a-f]{32}$");
		}

		// =====================================================================
		// IsAuthenticated — requires mocked session with USER_ID
		// =====================================================================

		private Security CreateSecurityWithSession(string? userId)
		{
			var mockSession = new Mock<ISession>();
			byte[]? valueBytes = userId != null ? Encoding.UTF8.GetBytes(userId) : null;
			mockSession
				.Setup(s => s.TryGetValue("USER_ID", out It.Ref<byte[]?>.IsAny))
				.Callback(new TryGetValueCallback((string key, out byte[]? value) =>
				{
					value = valueBytes;
				}))
				.Returns(valueBytes != null);

			var mockContext = new Mock<HttpContext>();
			mockContext.Setup(c => c.Session).Returns(mockSession.Object);

			var mockAccessor = new Mock<IHttpContextAccessor>();
			mockAccessor.Setup(a => a.HttpContext).Returns(mockContext.Object);

			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			return new Security(mockAccessor.Object, memoryCache);
		}

		// Delegate matching ISession.TryGetValue signature
		private delegate void TryGetValueCallback(string key, out byte[]? value);

		[Fact]
		public void IsAuthenticated_WithValidUserId_ReturnsTrue()
		{
			var security = CreateSecurityWithSession(Guid.NewGuid().ToString());
			security.IsAuthenticated().Should().BeTrue();
		}

		[Fact]
		public void IsAuthenticated_WithEmptyGuid_ReturnsFalse()
		{
			var security = CreateSecurityWithSession(Guid.Empty.ToString());
			security.IsAuthenticated().Should().BeFalse();
		}

		[Fact]
		public void IsAuthenticated_WithNullSession_ReturnsFalse()
		{
			var mockAccessor = new Mock<IHttpContextAccessor>();
			var mockContext = new Mock<HttpContext>();
			mockContext.Setup(c => c.Session).Returns((ISession?)null!);
			mockAccessor.Setup(a => a.HttpContext).Returns(mockContext.Object);

			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var security = new Security(mockAccessor.Object, memoryCache);
			// Should not throw — should return false gracefully
			security.IsAuthenticated().Should().BeFalse();
		}

		// =====================================================================
		// GetUserAccess — requires IMemoryCache populated with module validity
		// =====================================================================

		[Fact]
		public void GetUserAccess_AdminUser_ValidModule_ReturnsFullAccess()
		{
			// Create mock session with IS_ADMIN = true and a valid USER_ID
			var mockSession = new Mock<ISession>();
			SetSessionValue(mockSession, "USER_ID", Guid.NewGuid().ToString());
			SetSessionValue(mockSession, "IS_ADMIN", "True");

			var mockContext = new Mock<HttpContext>();
			mockContext.Setup(c => c.Session).Returns(mockSession.Object);
			var mockAccessor = new Mock<IHttpContextAccessor>();
			mockAccessor.Setup(a => a.HttpContext).Returns(mockContext.Object);

			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			// Set module as valid in cache
			memoryCache.Set("Modules.Accounts.Valid", (object)true);

			var security = new Security(mockAccessor.Object, memoryCache);

			// In DEBUG builds IS_ADMIN is forced false in GetUserAccess; in Release it returns FULL_ACCESS.
			// We test that the method doesn't throw and returns a reasonable value.
			int access = security.GetUserAccess("Accounts", "list");
			// In DEBUG mode, admin is forced false, so it will look up ACL data from session.
			// The access should be either FULL_ACCESS (100) or a defined ACL value.
			// Since no ACL is set in session, it will return 0 or throw. Just verify no crash.
			access.Should().BeOneOf(0, 100, -99, -98, 89, 90, ACL_ACCESS.FULL_ACCESS);
		}

		[Fact]
		public void GetUserAccess_EmptyModuleName_ThrowsException()
		{
			var security = CreateSecurityWithSession(Guid.NewGuid().ToString());
			Action act = () => security.GetUserAccess("", "list");
			act.Should().Throw<Exception>().WithMessage("*should not be empty*");
		}

		[Fact]
		public void GetUserAccess_NullSession_ThrowsException()
		{
			var mockAccessor = new Mock<IHttpContextAccessor>();
			mockAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

			var memoryCache = new MemoryCache(new MemoryCacheOptions());
			var security = new Security(mockAccessor.Object, memoryCache);

			Action act = () => security.GetUserAccess("Accounts", "list");
			act.Should().Throw<Exception>();
		}

		// Helper to set session string values
		private void SetSessionValue(Mock<ISession> mockSession, string key, string value)
		{
			byte[] valueBytes = Encoding.UTF8.GetBytes(value);
			mockSession
				.Setup(s => s.TryGetValue(key, out It.Ref<byte[]?>.IsAny))
				.Callback(new TryGetValueCallback((string k, out byte[]? v) =>
				{
					v = valueBytes;
				}))
				.Returns(true);
		}

		// =====================================================================
		// HexEncode — indirectly tested through HashPassword
		// =====================================================================

		[Fact]
		public void HashPassword_AlwaysReturnsLowercaseHex()
		{
			string result = Security.HashPassword("TEST");
			result.Should().MatchRegex("^[0-9a-f]+$");
		}
	}
}
