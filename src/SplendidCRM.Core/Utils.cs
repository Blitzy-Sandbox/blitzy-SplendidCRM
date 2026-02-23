/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	/// <summary>
	/// General utility methods for SplendidCRM.
	/// Migrated from SplendidCRM/_code/Utils.cs for .NET 10 ASP.NET Core.
	/// Replaces HttpContext.Current with IHttpContextAccessor, Application[] with IMemoryCache.
	/// </summary>
	public class Utils
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache;
		private readonly IConfiguration      _configuration;

		public Utils(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
		}

		/// <summary>
		/// Returns the maximum number of keys in the HTTP request form collection.
		/// </summary>
		public int MaxHttpCollectionKeys()
		{
			int nMax = Sql.ToInteger(_configuration["SplendidCRM:MaxHttpCollectionKeys"]);
			if (nMax == 0) nMax = SplendidDefaults.MaxHttpCollectionKeys();
			return nMax;
		}

		/// <summary>
		/// Builds a full URL from a relative path.
		/// </summary>
		public string BuildUrl(string sRelativePath)
		{
			var request = _httpContextAccessor?.HttpContext?.Request;
			if (request == null) return sRelativePath;
			string sScheme = request.Scheme;
			string sHost = request.Host.Value;
			string sBasePath = request.PathBase.Value ?? string.Empty;
			return sScheme + "://" + sHost + sBasePath + "/" + sRelativePath.TrimStart('/');
		}

		/// <summary>
		/// Encodes a value for safe inclusion in a URL query string.
		/// </summary>
		public static string EncodeURL(string sValue)
		{
			if (Sql.IsEmptyString(sValue)) return string.Empty;
			return Uri.EscapeDataString(sValue);
		}

		/// <summary>
		/// Returns the remote IP address of the current request.
		/// </summary>
		public string GetRemoteHost()
		{
			var context = _httpContextAccessor?.HttpContext;
			if (context == null) return string.Empty;
			return context.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
		}

		/// <summary>
		/// Returns the base path of the application.
		/// </summary>
		public string ApplicationPath()
		{
			var request = _httpContextAccessor?.HttpContext?.Request;
			if (request == null) return "/";
			return request.PathBase.Value ?? "/";
		}

		/// <summary>
		/// Checks if a string contains a valid email address format.
		/// </summary>
		public static bool IsValidEmail(string sEmail)
		{
			if (Sql.IsEmptyString(sEmail)) return false;
			try
			{
				var addr = new System.Net.Mail.MailAddress(sEmail);
				return addr.Address == sEmail;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the content root path from the web host environment.
		/// </summary>
		public string ContentRootPath()
		{
			return _configuration["ContentRoot"] ?? AppContext.BaseDirectory;
		}

		/// <summary>
		/// Split a comma-separated string into a list.
		/// </summary>
		public static List<string> SplitList(string sList)
		{
			List<string> list = new List<string>();
			if (!Sql.IsEmptyString(sList))
			{
				string[] arr = sList.Split(',');
				foreach (string s in arr)
				{
					string sTrimmed = s.Trim();
					if (!Sql.IsEmptyString(sTrimmed))
						list.Add(sTrimmed);
				}
			}
			return list;
		}
	}
}
