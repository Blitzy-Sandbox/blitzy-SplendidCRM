#nullable disable
using System;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// Mass update utility.
	/// Migrated from SplendidCRM/_code/MassUpdate.cs for .NET 10 ASP.NET Core.
	/// Replaces System.Web dependencies with ASP.NET Core equivalents.
	/// HttpContext.Current replaced with IHttpContextAccessor DI.
	/// Application[] replaced with IMemoryCache.
	/// </summary>
	public class MassUpdate
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache;
		private readonly IConfiguration      _configuration;

		public MassUpdate(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
		}
	}
}
