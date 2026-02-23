/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License 
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or 
 * using this file, you have unconditionally agreed to the terms and conditions of the License, 
 * including but not limited to restrictions on the number of users therein, and you may not use this 
 * file except in compliance with the License. 
 *********************************************************************************************************************/
#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Core CRM utility class — provides access to application configuration and module metadata.
	/// Migrated from SplendidCRM/_code/SplendidCRM.cs for .NET 10 ASP.NET Core.
	/// Replaces static access to Application["CONFIG.*"] with IMemoryCache and IConfiguration.
	/// </summary>
	public class Crm
	{
		private readonly IMemoryCache        _memoryCache       ;
		private readonly IConfiguration      _configuration     ;
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Constructor with dependency injection replacing HttpContext.Current and Application[] static access.
		/// </summary>
		public Crm(IMemoryCache memoryCache, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
		{
			_memoryCache        = memoryCache       ;
			_configuration      = configuration     ;
			_httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// Retrieves a configuration value from the cache, falling back to IConfiguration.
		/// Replaces: Application["CONFIG." + sName]
		/// </summary>
		public string Config(string sName)
		{
			string sValue = string.Empty;
			string sCacheKey = "CONFIG." + sName;
			if (_memoryCache.TryGetValue(sCacheKey, out object cached))
			{
				sValue = Sql.ToString(cached);
			}
			else
			{
				sValue = _configuration["SplendidCRM:" + sName] ?? string.Empty;
			}
			return sValue;
		}

		/// <summary>
		/// Retrieves a boolean configuration value.
		/// </summary>
		public bool Config(string sName, bool bDefault)
		{
			string sValue = Config(sName);
			if (Sql.IsEmptyString(sValue))
				return bDefault;
			return Sql.ToBoolean(sValue);
		}

		/// <summary>
		/// Retrieves an integer configuration value.
		/// </summary>
		public int Config(string sName, int nDefault)
		{
			string sValue = Config(sName);
			if (Sql.IsEmptyString(sValue))
				return nDefault;
			return Sql.ToInteger(sValue);
		}

		/// <summary>
		/// Retrieves a Guid configuration value.
		/// </summary>
		public Guid Config(string sName, Guid gDefault)
		{
			string sValue = Config(sName);
			if (Sql.IsEmptyString(sValue))
				return gDefault;
			return Sql.ToGuid(sValue);
		}

		/// <summary>
		/// The modules that are available in SplendidCRM.
		/// </summary>
		public static class Modules
		{
			public const string Accounts         = "Accounts"        ;
			public const string Bugs             = "Bugs"            ;
			public const string Calendar         = "Calendar"        ;
			public const string Calls            = "Calls"           ;
			public const string Campaigns        = "Campaigns"       ;
			public const string Cases            = "Cases"           ;
			public const string Contacts         = "Contacts"        ;
			public const string Documents        = "Documents"       ;
			public const string Emails           = "Emails"          ;
			public const string Employees        = "Employees"       ;
			public const string Leads            = "Leads"           ;
			public const string Meetings         = "Meetings"        ;
			public const string Notes            = "Notes"           ;
			public const string Opportunities    = "Opportunities"   ;
			public const string Project          = "Project"         ;
			public const string ProjectTask      = "ProjectTask"     ;
			public const string ProspectLists    = "ProspectLists"   ;
			public const string Prospects        = "Prospects"       ;
			public const string Tasks            = "Tasks"           ;
			public const string Users            = "Users"           ;
			public const string Teams            = "Teams"           ;
			public const string Roles            = "Roles"           ;
			public const string Import           = "Import"          ;
			public const string Administration   = "Administration"  ;
			public const string ACLRoles         = "ACLRoles"        ;
			public const string DynamicLayout    = "DynamicLayout"   ;
			public const string Releases         = "Releases"        ;
			public const string iFrames          = "iFrames"         ;
			public const string Feeds            = "Feeds"           ;
			public const string CampaignTrackers = "CampaignTrackers";
			public const string EmailMarketing   = "EmailMarketing"  ;
			public const string EmailTemplates   = "EmailTemplates"  ;
			public const string InboundEmail     = "InboundEmail"    ;
			public const string KBDocuments      = "KBDocuments"     ;
			public const string Forums           = "Forums"          ;
			public const string Schedulers       = "Schedulers"      ;
			public const string Terminology      = "Terminology"     ;
			public const string Tags             = "Tags"            ;
		}
	}
}
