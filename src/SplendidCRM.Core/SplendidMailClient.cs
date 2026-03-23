/**********************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc. 
 * Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved.
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>. 
 * 
 * You can contact SplendidCRM Software, Inc. at email address support@splendidcrm.com. 
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2011 SplendidCRM Software, Inc. All rights reserved."
 *********************************************************************************************************************/
// .NET 10 Migration: SplendidCRM/_code/SplendidMailClient.cs → src/SplendidCRM.Core/SplendidMailClient.cs
// Changes applied (minimal change clause — only framework migration changes):
//   1. REMOVED: using System.Web; (System.Web not available in .NET 10)
//   2. REPLACED: HttpApplicationState Application parameter in CreateMailClient()
//             → IMemoryCache memoryCache (Application[] → IMemoryCache cross-cutting migration, AAP §0.7.2)
//   3. CHANGED: class is non-abstract (virtual Send instead of abstract) to support DI registration
//              in Program.cs (AddSingleton<SplendidMailClient>() requires a concrete class).
//              The original abstract design intent is preserved via virtual/override hierarchy.
//   4. ADDED: DI constructor (IHttpContextAccessor, IMemoryCache, IConfiguration) for ASP.NET Core DI
//             registration, following the cross-cutting constructor-injection migration pattern.
//   5. ADDED: protected (IMemoryCache) constructor for concrete subclasses instantiated via factory.
//   6. PRESERVED: All mail transport selection logic (Office365, Exchange-Password, GoogleApps, SMTP default)
//   7. PRESERVED: namespace SplendidCRM, all public method signatures
//   8. NOTE: EmailUtils.CAMPAIGN_MANAGER_ID = Guid("00000000-0000-0000-0000-00000000000E") is preserved
//            inline as a constant because EmailUtils is not a dependency of this class; the value
//            is a well-known sentinel GUID defined in the original SplendidCRM codebase.
#nullable disable
using System;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM
{
	// 01/17/2017 Paul.  New SplendidMailClient object to encapsulate SMTP, Exchange and Google mail.
	public class SplendidMailClient
	{
		// =====================================================================================
		// .NET 10 Migration: DI fields replacing HttpApplicationState (Application) static access.
		// Protected visibility allows concrete subclasses (SplendidMailSmtp, etc.) to access
		// configuration and context without needing to redeclare their own fields.
		// =====================================================================================

		/// <summary>
		/// ASP.NET Core HttpContext accessor replacing HttpContext.Current static pattern.
		/// BEFORE: HttpContext.Current.Session["key"]
		/// AFTER:  _httpContextAccessor.HttpContext?.Session?.GetString("key")
		/// </summary>
		protected readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// In-process memory cache replacing HttpApplicationState (Application[]) state.
		/// BEFORE: Application["CONFIG.mail_sendtype"]
		/// AFTER:  _memoryCache.Get&lt;object&gt;("CONFIG.mail_sendtype")
		/// </summary>
		protected readonly IMemoryCache _memoryCache;

		/// <summary>
		/// Configuration accessor for appsettings.json and environment variable overrides.
		/// Replaces ConfigurationManager.AppSettings in .NET Framework.
		/// </summary>
		protected readonly IConfiguration _configuration;

		/// <summary>
		/// DI constructor — resolves SplendidMailClient from the ASP.NET Core service container.
		/// Used when Program.cs registers: builder.Services.AddSingleton&lt;SplendidMailClient&gt;()
		/// </summary>
		/// <param name="httpContextAccessor">
		///   Replaces HttpContext.Current static pattern for request/session access.
		/// </param>
		/// <param name="memoryCache">
		///   Replaces HttpApplicationState (Application[]) for shared in-process cache.
		/// </param>
		/// <param name="configuration">
		///   Provides access to appsettings.json and environment variable configuration.
		/// </param>
		public SplendidMailClient(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
			_configuration       = configuration;
		}

		/// <summary>
		/// Factory constructor — used by concrete subclasses instantiated via CreateMailClient().
		/// Accepts only IMemoryCache because the factory method receives only IMemoryCache,
		/// which is the minimum context required for reading mail transport configuration.
		/// </summary>
		/// <param name="memoryCache">
		///   In-process memory cache for reading CONFIG.* settings.
		/// </param>
		protected SplendidMailClient(IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
		}

		/// <summary>
		/// Sends the specified mail message via the configured transport.
		/// Concrete subclasses (SplendidMailSmtp, SplendidMailOffice365, etc.) override this
		/// method to implement transport-specific sending logic.
		///
		/// The base implementation throws an InvalidOperationException to indicate that the
		/// base class should not be used for sending — callers should obtain a concrete
		/// transport client via CreateMailClient(IMemoryCache).
		/// </summary>
		/// <param name="mail">The MailMessage to send.</param>
		/// <exception cref="InvalidOperationException">
		///   Always thrown by the base class — use CreateMailClient() to obtain a concrete client.
		/// </exception>
		public virtual void Send(MailMessage mail)
		{
			throw new InvalidOperationException(
				"SplendidMailClient.Send: Cannot send mail using the base class. " +
				"Obtain a concrete transport client via SplendidMailClient.CreateMailClient(IMemoryCache).");
		}

		// 01/18/2017 Paul.  This method will return the appropriate Campaign Manager client, based on configuration.
		// This is the global email sending account.
		/// <summary>
		/// Factory method that returns the appropriate concrete mail transport client based on
		/// the CONFIG.mail_sendtype value stored in the application memory cache.
		///
		/// Supported transport types:
		///   "Office365"        → SplendidMailOffice365 (OAuth 2.0 Microsoft Graph)
		///   "Exchange-Password"→ SplendidMailExchangePassword (Exchange EWS with credentials)
		///   "GoogleApps"       → SplendidMailGmail (OAuth 2.0 Google API)
		///   (default)          → SplendidMailSmtp (standard SMTP / MailKit)
		///
		/// .NET 10 Migration: HttpApplicationState Application parameter replaced with
		/// IMemoryCache memoryCache as part of the Application[] → IMemoryCache cross-cutting
		/// migration (AAP §0.7.2). The cache key "CONFIG.mail_sendtype" is preserved identically.
		/// </summary>
		/// <param name="memoryCache">
		///   In-process memory cache containing the CONFIG.* application settings.
		///   Read key: "CONFIG.mail_sendtype" to select the concrete mail transport.
		/// </param>
		/// <returns>A concrete <see cref="SplendidMailClient"/> for the configured transport.</returns>
		public static SplendidMailClient CreateMailClient(IMemoryCache memoryCache)
		{
			// .NET 10 Migration: Application["CONFIG.mail_sendtype"] → memoryCache.Get<object>("CONFIG.mail_sendtype")
			string sMAIL_SENDTYPE = Sql.ToString(memoryCache.Get<object>("CONFIG.mail_sendtype"));
			SplendidMailClient client = null;
			if ( String.Compare(sMAIL_SENDTYPE, "Office365", true) == 0 )
			{
				// ExchangeUtils.EXCHANGE_ID: well-known sentinel GUID identifying the Exchange/Office365
				// mailbox integration type. Preserved from original source; used as OAuth token bucket ID.
				client = new SplendidMailOffice365(memoryCache, ExchangeUtils.EXCHANGE_ID);
			}
			// 01/31/2017 Paul.  Add support for Exchange using Username/Password.
			else if ( String.Compare(sMAIL_SENDTYPE, "Exchange-Password", true) == 0 )
			{
				client = new SplendidMailExchangePassword(memoryCache);
			}
			else if ( String.Compare(sMAIL_SENDTYPE, "GoogleApps", true) == 0 )
			{
				// CAMPAIGN_MANAGER_ID: well-known sentinel GUID identifying the Campaign Manager
				// OAuth token bucket for Google API (EmailUtils.CAMPAIGN_MANAGER_ID in original source).
				// Value preserved inline: Guid("00000000-0000-0000-0000-00000000000E").
				// EmailUtils is not a dependency of SplendidMailClient to avoid circular coupling;
				// the constant value is hardcoded here per the minimal change clause.
				client = new SplendidMailGmail(memoryCache, new Guid("00000000-0000-0000-0000-00000000000E"));
			}
			else
			{
				// Default: standard SMTP transport (the only transport supported in Community Edition)
				client = new SplendidMailSmtp(memoryCache);
			}
			return client;
		}
	}
}
