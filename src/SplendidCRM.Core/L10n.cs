/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Data;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Localization/internationalization utility class.
	/// Migrated from SplendidCRM/_code/L10N.cs for .NET 10 ASP.NET Core.
	/// Replaces Application[] with IMemoryCache for terminology lookups.
	/// </summary>
	public class L10N
	{
		private readonly IMemoryCache _memoryCache;
		private string _sCultureName;

		public L10N(IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
			_sCultureName = SplendidDefaults.Culture();
		}

		public L10N(string sCulture, IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
			_sCultureName = sCulture ?? SplendidDefaults.Culture();
		}

		public string NAME
		{
			get { return _sCultureName; }
		}

		/// <summary>
		/// Returns the localized term for the given list name and term name.
		/// </summary>
		public string Term(string sLIST_NAME, string sTERM_NAME)
		{
			string sCacheKey = "TERMINOLOGY." + _sCultureName + "." + sLIST_NAME + "." + sTERM_NAME;
			if (_memoryCache.TryGetValue(sCacheKey, out object cached))
			{
				return Sql.ToString(cached);
			}
			// Fall back to en-US if the requested culture is not found.
			if (_sCultureName != "en-US")
			{
				sCacheKey = "TERMINOLOGY.en-US." + sLIST_NAME + "." + sTERM_NAME;
				if (_memoryCache.TryGetValue(sCacheKey, out object cachedFallback))
				{
					return Sql.ToString(cachedFallback);
				}
			}
			return sTERM_NAME;
		}

		/// <summary>
		/// Returns the localized term for a module-specific label.
		/// </summary>
		public string Term(string sMODULE_NAME, string sLIST_NAME, string sTERM_NAME)
		{
			string sCacheKey = "TERMINOLOGY." + _sCultureName + "." + sMODULE_NAME + "." + sLIST_NAME + "." + sTERM_NAME;
			if (_memoryCache.TryGetValue(sCacheKey, out object cached))
			{
				return Sql.ToString(cached);
			}
			return Term(sLIST_NAME, sTERM_NAME);
		}

		/// <summary>
		/// Returns the localized term for a simple key (used for UI labels).
		/// </summary>
		public string Term(string sTERM)
		{
			return Term(string.Empty, sTERM);
		}

		/// <summary>
		/// Gets the CultureInfo for the current language.
		/// </summary>
		public CultureInfo GetCultureInfo()
		{
			try
			{
				return CultureInfo.CreateSpecificCulture(_sCultureName);
			}
			catch
			{
				return CultureInfo.CreateSpecificCulture("en-US");
			}
		}
	}
}
