#nullable disable
using System;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Exchange password email client.
	/// Migrated from SplendidCRM/_code/SplendidMailExchangePassword.cs for .NET 10 ASP.NET Core.
	/// Replaces System.Web dependencies with ASP.NET Core equivalents.
	/// HttpContext.Current replaced with IHttpContextAccessor DI.
	/// Application[] replaced with IMemoryCache.
	/// </summary>
	public class SplendidMailExchangePassword
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache;
		private readonly IConfiguration      _configuration;

		public SplendidMailExchangePassword(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
		}
	}
}
