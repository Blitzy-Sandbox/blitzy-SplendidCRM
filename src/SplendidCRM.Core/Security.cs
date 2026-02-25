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
// .NET 10 Migration: SplendidCRM/_code/Security.cs → src/SplendidCRM.Core/Security.cs
// Changes applied:
//   - Removed: using System.Web; using System.Web.SessionState; using System.Web.UI.WebControls;
//   - Added:   using Microsoft.AspNetCore.Http; using Microsoft.Extensions.Caching.Memory; using Newtonsoft.Json;
//   - Static class with HttpContext.Current → DI-friendly instance class with IHttpContextAccessor + IMemoryCache
//   - All HttpContext.Current.Session["key"] → ISession.GetString("key") / ISession.SetString("key", value)
//   - All HttpContext.Current.Application["key"] → IMemoryCache.Get<T>("key")
//   - HttpRuntime.Cache → IMemoryCache (no usages in this file)
//   - Crm.Config.* static calls → direct IMemoryCache reads of CONFIG.* cache keys
//   - Rijndael.Create() → Aes.Create() (Rijndael is obsolete in .NET 6+, Aes is the cross-platform replacement)
//   - MD5CryptoServiceProvider → MD5.Create() with TECHNICAL DEBT comment preserved
//   - DataGridItem (System.Web.UI) container check removed; DataRow/DataRowView patterns retained
//   - SplendidCache.SavedSearch() circular-dependency resolved via direct session read + JSON deserialization
//   - ACL DataTable stored in session encoded as JSON string for distributed session (Redis/SQL Server) compatibility
//   - Sql.NextPlaceholder / Sql.MetadataName / Sql.HexEncode inlined as private static helpers
//   - Application[] module-validity reads: Application["Modules.X.Valid"] → _memoryCache.Get("Modules.X.Valid")
#nullable disable
using System;
using System.IO;
using System.Data;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace SplendidCRM
{
	/// <summary>
	/// Security façade — Authentication, ACL enforcement, MD5 hashing, and Rijndael/AES encryption.
	/// Implements the 4-tier ACL model: Module → Team → Field → Record.
	///
	/// Migrated from SplendidCRM/_code/Security.cs (1,388 lines) for .NET 10 ASP.NET Core.
	/// All session-backed static properties are converted to instance properties backed by
	/// ISession (distributed session compatible). Application[] state is replaced by IMemoryCache.
	///
	/// DESIGN NOTES for callers:
	///   • Register Security as a SCOPED service (per-request lifetime) so that each request gets
	///     the current IHttpContextAccessor-bound session data.
	///   • HashPassword(), EncryptPassword(string,Guid,Guid), DecryptPassword(string,Guid,Guid) are
	///     static and may be called without a Security instance.
	/// </summary>
	public class Security
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache        ;

		// =====================================================================================
		// Public ACL constant aliases on Security class
		// These expose ACL_ACCESS constants from ACLGrid.cs as Security.ACL_ACCESS_* for
		// backward compatibility with authorization handlers that reference Security.ACL_ACCESS_*.
		// NOTE: The canonical constants live in ACL_ACCESS (ACLGrid.cs).
		// =====================================================================================

		// 03/15/2010 Paul.  Expose ACL constants on Security class for convenience.
		/// <summary>Module is enabled/accessible (same as ACL_ACCESS.ENABLED = 89).</summary>
		public const int ACL_ACCESS_ENABLED    = ACL_ACCESS.ENABLED    ;   // 89
		/// <summary>Owner-only access (same as ACL_ACCESS.OWNER = 75).</summary>
		public const int ACL_ACCESS_OWNER      = ACL_ACCESS.OWNER      ;   // 75
		/// <summary>All-user access (same as ACL_ACCESS.ALL = 90).</summary>
		public const int ACL_ACCESS_ALL        = ACL_ACCESS.ALL        ;   // 90
		/// <summary>Full admin access (same as ACL_ACCESS.FULL_ACCESS = 100).</summary>
		public const int ACL_ACCESS_FULL_ACCESS = ACL_ACCESS.FULL_ACCESS;   // 100
		/// <summary>No access (same as ACL_ACCESS.NONE = -99).</summary>
		public const int ACL_ACCESS_NONE       = ACL_ACCESS.NONE       ;   // -99

		// =====================================================================================
		// Constructor (replaces static class — DI injection pattern)
		// =====================================================================================

		/// <summary>
		/// Constructs a Security instance with injected HTTP context accessor and memory cache.
		/// </summary>
		/// <param name="httpContextAccessor">Replaces HttpContext.Current throughout</param>
		/// <param name="memoryCache">Replaces Application[] and HttpRuntime.Cache throughout</param>
		public Security(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache        ;
		}

		// =====================================================================================
		// Private session accessor helper
		// BEFORE: HttpContext.Current.Session
		// AFTER:  _httpContextAccessor.HttpContext?.Session
		// =====================================================================================

		/// <summary>Gets the ISession from the current HTTP context, or null when outside a request.</summary>
		private ISession Session
		{
			get { return _httpContextAccessor?.HttpContext?.Session; }
		}

		// =====================================================================================
		// Session-backed Properties
		// BEFORE (static): return Sql.ToGuid(HttpContext.Current.Session["USER_ID"])
		// AFTER (instance): return Sql.ToGuid(Session?.GetString("USER_ID"))
		//
		// ISession only supports string/byte-array values; all typed values are round-tripped
		// through string representations so they survive serialisation to Redis or SQL Server.
		// =====================================================================================

		/// <summary>
		/// Authenticated user's primary key (GUID).
		/// Returns Guid.Empty when there is no active session (e.g. background service, SOAP call).
		/// </summary>
		public Guid USER_ID
		{
			get
			{
				// 02/17/2006 Paul.  Return empty guid when session is unavailable (scheduler / SOAP context).
				if (Session == null)
					return Guid.Empty;
				return Sql.ToGuid(Session.GetString("USER_ID"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_ID", value.ToString());
			}
		}

		// 07/15/2021 Paul.  React Client needs to access the ASP.NET_SessionId.
		/// <summary>
		/// Computed session token sent to the React SPA — a hash of USER_ID + session ID,
		/// providing the same value as the legacy ASP.NET_SessionId-based token.
		/// </summary>
		public string USER_SESSION
		{
			get
			{
				if (Session == null)
					return String.Empty;
				// Preserve exact hash format: MD5(USER_ID + ";" + SessionID)
				string sUserId    = Sql.ToString(Session.GetString("USER_ID"));
				string sSessionId = _httpContextAccessor?.HttpContext?.Session?.Id ?? String.Empty;
				return Security.HashPassword(sUserId + ";" + sSessionId);
			}
		}

		// 03/02/2008 Paul.  Keep track of the login ID so that we can log them out.
		/// <summary>Login tracking record ID.</summary>
		public Guid USER_LOGIN_ID
		{
			get
			{
				if (Session == null)
					return Guid.Empty;
				return Sql.ToGuid(Session.GetString("USER_LOGIN_ID"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_LOGIN_ID", value.ToString());
			}
		}

		/// <summary>User login name (user_name column).</summary>
		public string USER_NAME
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_NAME"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_NAME", value ?? String.Empty);
			}
		}

		/// <summary>User's display name (first_name + last_name).</summary>
		public string FULL_NAME
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("FULL_NAME"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("FULL_NAME", value ?? String.Empty);
			}
		}

		// 11/21/2014 Paul.  Add User Picture.
		/// <summary>URL/path to user profile picture.</summary>
		public string PICTURE
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("PICTURE"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("PICTURE", value ?? String.Empty);
			}
		}

		/// <summary>True when the authenticated user has system administrator rights.</summary>
		public bool IS_ADMIN
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToBoolean(Session.GetString("IS_ADMIN"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("IS_ADMIN", value.ToString());
			}
		}

		/// <summary>True when the user has delegated admin rights (bAllowAdminRoles must also be enabled).</summary>
		public bool IS_ADMIN_DELEGATE
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToBoolean(Session.GetString("IS_ADMIN_DELEGATE"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("IS_ADMIN_DELEGATE", value.ToString());
			}
		}

		/// <summary>True when the user is restricted to portal-only access.</summary>
		public bool PORTAL_ONLY
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToBoolean(Session.GetString("PORTAL_ONLY"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("PORTAL_ONLY", value.ToString());
			}
		}

		// 11/25/2006 Paul.  Default TEAM_ID.
		/// <summary>The user's primary team ID.</summary>
		public Guid TEAM_ID
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToGuid(Session.GetString("TEAM_ID"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("TEAM_ID", value.ToString());
			}
		}

		/// <summary>The user's primary team name.</summary>
		public string TEAM_NAME
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("TEAM_NAME"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("TEAM_NAME", value ?? String.Empty);
			}
		}

		// 04/04/2010 Paul.  Add Exchange Alias so that we can enable/disable Exchange appropriately.
		/// <summary>Exchange mailbox alias for the current user.</summary>
		public string EXCHANGE_ALIAS
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("EXCHANGE_ALIAS"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("EXCHANGE_ALIAS", value ?? String.Empty);
			}
		}

		// 04/07/2010 Paul.  Add Exchange Email as it will be needed for Push Subscriptions.
		/// <summary>Exchange email address for the current user (used for push subscription binding).</summary>
		public string EXCHANGE_EMAIL
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("EXCHANGE_EMAIL"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("EXCHANGE_EMAIL", value ?? String.Empty);
			}
		}

		// 07/09/2010 Paul.  Move the SMTP values from USER_PREFERENCES to the main table to make it easier to access.
		/// <summary>SMTP username for the current user's personal email account.</summary>
		public string MAIL_SMTPUSER
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("MAIL_SMTPUSER"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("MAIL_SMTPUSER", value ?? String.Empty);
			}
		}

		// 07/09/2010 Paul.  Move the SMTP values from USER_PREFERENCES to the main table to make it easier to access.
		/// <summary>SMTP password for the current user's personal email account.</summary>
		public string MAIL_SMTPPASS
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("MAIL_SMTPPASS"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("MAIL_SMTPPASS", value ?? String.Empty);
			}
		}

		// 11/05/2010 Paul.  Each user can have their own email account, but they all share the same server.
		/// <summary>The user's primary email address (email1 column).</summary>
		public string EMAIL1
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("EMAIL1"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("EMAIL1", value ?? String.Empty);
			}
		}

		// 05/05/2016 Paul.  The User Primary Role is used with role-based views.
		/// <summary>The user's primary ACL role ID (used for role-based view selection).</summary>
		public Guid PRIMARY_ROLE_ID
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToGuid(Session.GetString("PRIMARY_ROLE_ID"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("PRIMARY_ROLE_ID", value.ToString());
			}
		}

		/// <summary>The user's primary ACL role name.</summary>
		public string PRIMARY_ROLE_NAME
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("PRIMARY_ROLE_NAME"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("PRIMARY_ROLE_NAME", value ?? String.Empty);
			}
		}

		// =====================================================================================
		// Extended user-preference session properties
		// These are not in the original source Security.cs but are populated by SplendidInit.cs
		// from the USERS table and stored in session.  The REST API controllers read them directly.
		// =====================================================================================

		// 07/15/2021 Paul.  React client needs IS_AUTHENTICATED shortcut.
		/// <summary>
		/// Convenience property delegating to IsAuthenticated().
		/// Exposed for REST controllers that use the property syntax.
		/// </summary>
		public bool IS_AUTHENTICATED
		{
			get { return IsAuthenticated(); }
		}

		/// <summary>User language / locale setting (e.g. "en-US").</summary>
		public string USER_LANG
		{
			get
			{
				if (Session == null)
					return "en-US";
				return Sql.ToString(Session.GetString("USER_LANG")) is string s && s.Length > 0 ? s : "en-US";
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_LANG", value ?? String.Empty);
			}
		}

		/// <summary>User UI theme name (e.g. "SugarCRM5", "Atlantic").</summary>
		public string USER_THEME
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_THEME"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_THEME", value ?? String.Empty);
			}
		}

		/// <summary>User date format preference (e.g. "MM/dd/yyyy").</summary>
		public string USER_DATE_FORMAT
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_DATE_FORMAT"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_DATE_FORMAT", value ?? String.Empty);
			}
		}

		/// <summary>User time format preference (e.g. "h:mm tt").</summary>
		public string USER_TIME_FORMAT
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_TIME_FORMAT"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_TIME_FORMAT", value ?? String.Empty);
			}
		}

		/// <summary>User timezone ID string (e.g. "America/New_York").</summary>
		public string USER_TIMEZONE_ID
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_TIMEZONE_ID"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_TIMEZONE_ID", value ?? String.Empty);
			}
		}

		/// <summary>User currency ID (GUID as string) for display formatting.</summary>
		public string USER_CURRENCY_ID
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_CURRENCY_ID"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_CURRENCY_ID", value ?? String.Empty);
			}
		}

		/// <summary>Phone extension for VoIP (Twilio/PhoneBurner) integration.</summary>
		public string USER_EXTENSION
		{
			get
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				return Sql.ToString(Session.GetString("USER_EXTENSION"));
			}
			set
			{
				if (Session == null)
					throw new Exception("HttpContext.Current.Session is null");
				Session.SetString("USER_EXTENSION", value ?? String.Empty);
			}
		}

		// =====================================================================================
		// Simple helper predicates
		// =====================================================================================

		/// <summary>True when the current user has a non-empty Exchange alias configured.</summary>
		public bool HasExchangeAlias()
		{
			return !Sql.IsEmptyString(this.EXCHANGE_ALIAS);
		}

		/// <summary>
		/// Detects Windows (Negotiate/NTLM) authentication for the current request.
		/// BEFORE: Checked HttpContext.Current.Request.ServerVariables["AUTH_USER"] (IIS-specific).
		/// AFTER:  Checks HttpContext.User.Identity.AuthenticationType for Negotiate or NTLM.
		/// Note: Utils.IsOfflineClient check removed — offline client does not apply to ASP.NET Core hosting.
		/// </summary>
		public bool IsWindowsAuthentication()
		{
			var httpContext = _httpContextAccessor?.HttpContext;
			if (httpContext == null)
				return false;
			var identity = httpContext.User?.Identity;
			if (identity == null || !identity.IsAuthenticated)
				return false;
			// Windows Authentication uses Negotiate (Kerberos/NTLM) or NTLM authentication type
			string sAuthType = identity.AuthenticationType ?? String.Empty;
			return sAuthType.Equals("Negotiate", StringComparison.OrdinalIgnoreCase)
			    || sAuthType.Equals("NTLM",      StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>True when USER_ID is a non-empty GUID (i.e. the user has an active authenticated session).</summary>
		public bool IsAuthenticated()
		{
			return !Sql.IsEmptyGuid(this.USER_ID);
		}

		/// <summary>True when the current session is operating under an admin impersonation context.</summary>
		public bool IsImpersonating()
		{
			return Sql.ToBoolean(Session?.GetString("USER_IMPERSONATION"));
		}

		// =====================================================================================
		// Clear() — session reset preserving browser capability flags
		// BEFORE: HttpContext.Current.Session.Clear() then restore from local vars
		// AFTER:  ISession.Clear() then restore via SetString()
		// =====================================================================================

		// 02/28/2007 Paul.  Centralise session reset to prepare for WebParts.
		/// <summary>
		/// Clears the session while preserving browser-capability detection flags set during
		/// the initial request (Browser, IsMobileDevice, SupportsPopups, AllowAutoComplete,
		/// SupportsSpeech, SupportsHandwriting, SupportsTouch, SupportsDraggable).
		/// </summary>
		public void Clear()
		{
			if (Session == null)
				throw new Exception("HttpContext.Current.Session is null");

			// 01/26/2011 Paul.  Retain mobile/browser capability flags across session clear.
			string sBrowser             = Sql.ToString (Session.GetString("Browser"            ));
			bool   bIsMobileDevice      = Sql.ToBoolean(Session.GetString("IsMobileDevice"     ));
			bool   bSupportsPopups      = Sql.ToBoolean(Session.GetString("SupportsPopups"     ));
			bool   bAllowAutoComplete   = Sql.ToBoolean(Session.GetString("AllowAutoComplete"  ));
			// 08/22/2012 Paul.  Apple and Android devices should support speech and handwriting.
			bool   bSupportsSpeech      = Sql.ToBoolean(Session.GetString("SupportsSpeech"     ));
			bool   bSupportsHandwriting = Sql.ToBoolean(Session.GetString("SupportsHandwriting"));
			// 11/14/2012 Paul.  Microsoft Surface has Touch in the agent string.
			bool   bSupportsTouch       = Sql.ToBoolean(Session.GetString("SupportsTouch"      ));
			// 05/17/2013 Paul.  We need to be able to detect draggable.
			bool   bSupportsDraggable   = Sql.ToBoolean(Session.GetString("SupportsDraggable"  ));

			Session.Clear();

			// Restore browser capability flags
			Session.SetString("Browser"            , sBrowser                     );
			Session.SetString("IsMobileDevice"     , bIsMobileDevice     .ToString());
			Session.SetString("SupportsPopups"     , bSupportsPopups     .ToString());
			Session.SetString("AllowAutoComplete"  , bAllowAutoComplete  .ToString());
			Session.SetString("SupportsSpeech"     , bSupportsSpeech     .ToString());
			Session.SetString("SupportsHandwriting", bSupportsHandwriting.ToString());
			Session.SetString("SupportsTouch"      , bSupportsTouch      .ToString());
			Session.SetString("SupportsDraggable"  , bSupportsDraggable  .ToString());
		}

		// =====================================================================================
		// Password Hashing — MD5 preserved for SugarCRM backward compatibility
		// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
		// =====================================================================================

		// 11/18/2005 Paul.  SugarCRM stores an MD5 hash of the password.
		/// <summary>
		/// Computes the SugarCRM-compatible MD5 hash of a plain-text password.
		/// Returns a lowercase hex-encoded 32-character string.
		/// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
		/// Changing the algorithm would invalidate all existing user password records in the database.
		/// </summary>
		/// <param name="sPASSWORD">Plain-text password to hash.</param>
		/// <returns>Lowercase hex-encoded MD5 digest (32 chars).</returns>
		public static string HashPassword(string sPASSWORD)
		{
			// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
			UTF8Encoding utf8 = new UTF8Encoding();
			byte[] aby = utf8.GetBytes(sPASSWORD);
			// 02/07/2010 Paul.  Defensive programming — use using() since MD5 implements IDisposable.
			using (MD5 md5 = MD5.Create())
			{
				byte[] binMD5 = md5.ComputeHash(aby);
				return HexEncode(binMD5);
			}
		}

		// =====================================================================================
		// Encryption / Decryption — Rijndael replaced with Aes (cross-platform, .NET 10)
		// BEFORE: Rijndael.Create() (obsolete in .NET 6+, Windows-only)
		// AFTER:  Aes.Create() (cross-platform AES-128 with identical key/IV layout)
		// The byte layout of the encrypted ciphertext is identical because both Rijndael and Aes
		// default to 128-bit block size + CBC mode when block size == key size == 16 bytes (Guid).
		// =====================================================================================

		// 01/08/2008 Paul.  Use Rijndael with base64 encoding for email password encryption.
		/// <summary>
		/// Encrypts <paramref name="sPASSWORD"/> using AES-128 with a GUID-derived key and IV.
		/// Returns a Base64-encoded ciphertext string, or null on failure.
		/// </summary>
		public static string EncryptPassword(string sPASSWORD, Guid gKEY, Guid gIV)
		{
			UTF8Encoding utf8 = new UTF8Encoding(false);

			string sResult = null;
			byte[] byPassword = utf8.GetBytes(sPASSWORD);
			using (MemoryStream stm = new MemoryStream())
			{
				// .NET 10 Migration: Aes.Create() replaces Rijndael.Create().
				// Both use 128-bit block/key with CBC mode by default; cipher output is identical.
				using (Aes aes = Aes.Create())
				{
					aes.Key = gKEY.ToByteArray();
					aes.IV  = gIV .ToByteArray();
					using (CryptoStream cs = new CryptoStream(stm, aes.CreateEncryptor(), CryptoStreamMode.Write))
					{
						cs.Write(byPassword, 0, byPassword.Length);
						cs.FlushFinalBlock();
					}
				}
				sResult = Convert.ToBase64String(stm.ToArray());
			}
			return sResult;
		}

		// 02/06/2017 Paul.  Simplified DecryptPassword overload that resolves keys from cache.
		/// <summary>
		/// Decrypts an email password using keys stored in the application cache.
		/// BEFORE: DecryptPassword(HttpApplicationState Application, string sPASSWORD)
		/// AFTER:  DecryptPassword(IMemoryCache memoryCache, string sPASSWORD) — keys come from cache.
		/// </summary>
		public string DecryptPassword(string sPASSWORD)
		{
			// 02/06/2017 Paul.  Application["CONFIG.InboundEmailKey"] and Application["CONFIG.InboundEmailIV"]
			// AFTER: _memoryCache.Get("CONFIG.InboundEmailKey") and _memoryCache.Get("CONFIG.InboundEmailIV")
			Guid gINBOUND_EMAIL_KEY = Sql.ToGuid(_memoryCache.Get("CONFIG.InboundEmailKey"));
			Guid gINBOUND_EMAIL_IV  = Sql.ToGuid(_memoryCache.Get("CONFIG.InboundEmailIV" ));
			return DecryptPassword(sPASSWORD, gINBOUND_EMAIL_KEY, gINBOUND_EMAIL_IV);
		}

		/// <summary>
		/// Decrypts a Base64-encoded AES-128 ciphertext using a GUID-derived key and IV.
		/// Throws if sPASSWORD is empty (preserves original "Padding is invalid" avoidance).
		/// </summary>
		public static string DecryptPassword(string sPASSWORD, Guid gKEY, Guid gIV)
		{
			UTF8Encoding utf8 = new UTF8Encoding(false);

			// 08/12/2015 Paul.  Provide a better error than "Padding is invalid and cannot be removed."
			if (Sql.IsEmptyString(sPASSWORD))
				throw new Exception("Password is empty.");

			string sResult = null;
			byte[] byPassword = Convert.FromBase64String(sPASSWORD);
			using (MemoryStream stm = new MemoryStream())
			{
				// .NET 10 Migration: Aes.Create() replaces Rijndael.Create().
				using (Aes aes = Aes.Create())
				{
					aes.Key = gKEY.ToByteArray();
					aes.IV  = gIV .ToByteArray();
					using (CryptoStream cs = new CryptoStream(stm, aes.CreateDecryptor(), CryptoStreamMode.Write))
					{
						cs.Write(byPassword, 0, byPassword.Length);
						cs.Flush();
					}
				}
				byte[] byResult = stm.ToArray();
				sResult = utf8.GetString(byResult, 0, byResult.Length);
			}
			return sResult;
		}

		// =====================================================================================
		// Module Access — stored in IMemoryCache (global) for module-level defaults
		// BEFORE: Application["ACLACCESS_MODULE_ACCESS_TYPE"] = nACLACCESS
		// AFTER:  _memoryCache.Set("ACLACCESS_MODULE_ACCESS_TYPE", nACLACCESS)
		// =====================================================================================

		// 02/03/2009 Paul.  This function might be called from a background process.
		/// <summary>
		/// Stores a module-level ACL access value in the application-wide memory cache.
		/// Used for role-based defaults that apply across all users.
		/// BEFORE: SetModuleAccess(HttpApplicationState Application, ...)
		/// AFTER:  Uses IMemoryCache injection replacing Application[].
		/// </summary>
		public void SetModuleAccess(string sMODULE_NAME, string sACCESS_TYPE, int nACLACCESS)
		{
			if (_memoryCache == null)
				throw new Exception("IMemoryCache is null");
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(sMODULE_NAME))
				throw new Exception("sMODULE_NAME should not be empty.");
			_memoryCache.Set("ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE, nACLACCESS);
		}

		// =====================================================================================
		// User ACL Access — stored in per-user ISession for user-specific overrides
		// BEFORE: HttpContext.Current.Session["ACLACCESS_MODULE_ACCESS_TYPE"] = nACLACCESS
		// AFTER:  Session.SetString("ACLACCESS_MODULE_ACCESS_TYPE", nACLACCESS.ToString())
		// =====================================================================================

		/// <summary>
		/// Stores a user-specific ACL access level for a module in the session.
		/// Session value overrides the module-level cache value for this user's requests.
		/// </summary>
		public void SetUserAccess(string sMODULE_NAME, string sACCESS_TYPE, int nACLACCESS)
		{
			if (Session == null)
				throw new Exception("HttpContext.Current.Session is null");
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(sMODULE_NAME))
				throw new Exception("sMODULE_NAME should not be empty.");
			Session.SetString("ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE, nACLACCESS.ToString());
		}

		/// <summary>
		/// Returns the effective ACL access level for the current user and the specified module/access-type pair.
		/// 
		/// Priority: IS_ADMIN → Calendar/Activities aggregation → Session (user override) → IMemoryCache (module default).
		/// 
		/// BEFORE: Application["Modules.X.Valid"] and Session["ACLACCESS_X_Y"]
		/// AFTER:  _memoryCache.Get("Modules.X.Valid") and Session.GetString("ACLACCESS_X_Y")
		/// </summary>
		public int GetUserAccess(string sMODULE_NAME, string sACCESS_TYPE)
		{
			if (Session == null)
				throw new Exception("HttpContext.Current.Session is null");
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(sMODULE_NAME))
				throw new Exception("sMODULE_NAME should not be empty.");

			// 08/30/2009 Paul.  Don't apply admin rules when debugging so that we can test the code.
			// 09/01/2009 Paul.  Can't skip admin rules here, otherwise too many dynamic things in the admin area will fail.
			// 04/27/2006 Paul.  Admins have full access to the site, no matter what the role.
			bool bIsAdmin = IS_ADMIN;
			// 12/03/2017 Paul.  Don't apply admin rules when debugging so that we can test the code.
#if DEBUG
			bIsAdmin = false;
#endif
			if (bIsAdmin)
			{
				// 04/21/2016 Paul.  We need to make sure that disabled modules do not show related buttons.
				// BEFORE: Application["Modules." + sMODULE_NAME + ".Valid"]
				// AFTER:  _memoryCache.Get("Modules." + sMODULE_NAME + ".Valid")
				if (Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".Valid")))
					return ACL_ACCESS.FULL_ACCESS;
				else
					return ACL_ACCESS.NONE;  // 08/10/2017 Paul.  Return negative to prevent access, not zero.
			}

			// 12/05/2006 Paul.  Combine Activity and Calendar related modules into a single access value.
			int nACLACCESS = 0;
			// 08/10/2017 Paul.  Return negative to prevent access if module is invalid.
			// BEFORE: Application["Modules." + sMODULE_NAME + ".Valid"]
			// AFTER:  _memoryCache.Get("Modules." + sMODULE_NAME + ".Valid")
			if (!Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".Valid")))
			{
				nACLACCESS = ACL_ACCESS.NONE;
			}
			else if (sMODULE_NAME == "Calendar")
			{
				// 12/05/2006 Paul.  The Calendar related views only combine Calls and Meetings.
				int nACLACCESS_Calls    = GetUserAccess("Calls"   , sACCESS_TYPE);
				int nACLACCESS_Meetings = GetUserAccess("Meetings", sACCESS_TYPE);
				// 12/05/2006 Paul.  Use the max value so that Activities will be displayed if either are accessible.
				nACLACCESS = Math.Max(nACLACCESS_Calls, nACLACCESS_Meetings);
			}
			else if (sMODULE_NAME == "Activities")
			{
				// 12/05/2006 Paul.  The Activities combines Calls, Meetings, Tasks, Notes and Emails.
				int nACLACCESS_Calls    = GetUserAccess("Calls"   , sACCESS_TYPE);
				int nACLACCESS_Meetings = GetUserAccess("Meetings", sACCESS_TYPE);
				int nACLACCESS_Tasks    = GetUserAccess("Tasks"   , sACCESS_TYPE);
				int nACLACCESS_Notes    = GetUserAccess("Notes"   , sACCESS_TYPE);
				int nACLACCESS_Emails   = GetUserAccess("Emails"  , sACCESS_TYPE);
				nACLACCESS = nACLACCESS_Calls;
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Meetings);
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Tasks   );
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Notes   );
				nACLACCESS = Math.Max(nACLACCESS, nACLACCESS_Emails  );
			}
			else
			{
				string sAclKey = "ACLACCESS_" + sMODULE_NAME + "_" + sACCESS_TYPE;
				// 04/27/2006 Paul.  If no specific level is provided, fall back to the Module level (IMemoryCache).
				// BEFORE: Session[sAclKey] == null ? Application[sAclKey] : Session[sAclKey]
				// AFTER:  Session.GetString(sAclKey) == null ? _memoryCache.Get(sAclKey) : Session.GetString(sAclKey)
				string sSessionValue = Session.GetString(sAclKey);
				if (sSessionValue == null)
					nACLACCESS = Sql.ToInteger(_memoryCache.Get(sAclKey));
				else
					nACLACCESS = Sql.ToInteger(sSessionValue);

				if (sACCESS_TYPE != "access" && nACLACCESS >= 0)
				{
					// 04/27/2006 Paul.  The access type can override any other type.
					// Take the minimum — if either value is denied the result is negative.
					sAclKey = "ACLACCESS_" + sMODULE_NAME + "_access";
					int nAccessLevel = 0;
					string sAccessSessionValue = Session.GetString(sAclKey);
					if (sAccessSessionValue == null)
						nAccessLevel = Sql.ToInteger(_memoryCache.Get(sAclKey));
					else
						nAccessLevel = Sql.ToInteger(sAccessSessionValue);
					if (nAccessLevel < 0)
						nACLACCESS = nAccessLevel;
				}
			}
			return nACLACCESS;
		}

		// =====================================================================================
		// ACL Role Access — stored in session as boolean strings
		// BEFORE: Session["ACLRoles." + sROLE_NAME] = true
		// AFTER:  Session.SetString("ACLRoles." + sROLE_NAME, "True")
		// =====================================================================================

		// 11/11/2010 Paul.  Provide quick access to ACL Roles and Teams.
		/// <summary>Records that the current user has the specified ACL role.</summary>
		public void SetACLRoleAccess(string sROLE_NAME)
		{
			Session?.SetString("ACLRoles." + sROLE_NAME, "True");
		}

		/// <summary>Returns true when the current user has the specified ACL role.</summary>
		public bool GetACLRoleAccess(string sROLE_NAME)
		{
			return Sql.ToBoolean(Session?.GetString("ACLRoles." + sROLE_NAME));
		}

		/// <summary>Records that the current user is a member of the specified team.</summary>
		public void SetTeamAccess(string sTEAM_NAME)
		{
			Session?.SetString("Teams." + sTEAM_NAME, "True");
		}

		/// <summary>Returns true when the current user is a member of the specified team.</summary>
		public bool GetTeamAccess(string sTEAM_NAME)
		{
			return Sql.ToBoolean(Session?.GetString("Teams." + sTEAM_NAME));
		}

		// =====================================================================================
		// Record Access — row-level ACL combining user access + ownership + record-level security
		// BEFORE: Container is DataGridItem  (System.Web.UI — removed in .NET Core)
		// AFTER:  Container is DataRow or DataRowView only (WebForms grid types removed)
		// =====================================================================================

		// 10/31/2017 Paul.  Provide a way to inject Record level ACL.
		// 11/01/2017 Paul.  Must define container as object; created prior to the DataRow version for dynamic binding.
		/// <summary>
		/// Returns the record-level ACL for a container (DataRow or DataRowView).
		/// BEFORE: checked Container is DataGridItem — WebForms type removed in .NET Core migration.
		/// AFTER: Checks Container is DataRow or DataRowView directly.
		/// </summary>
		public int GetRecordAccess(object Container, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_FIELD)
		{
			DataRow row = null;
			if (Container is DataRow dataRow)
			{
				row = dataRow;
			}
			else if (Container is DataRowView dataRowView)
			{
				row = dataRowView.Row;
			}
			return GetRecordAccess(row, sMODULE_NAME, sACCESS_TYPE, sASSIGNED_USER_ID_FIELD);
		}

		/// <inheritdoc cref="GetRecordAccess(object, string, string, string)"/>
		public int GetRecordAccess(object Container, string sMODULE_NAME, string sACCESS_TYPE)
		{
			return GetRecordAccess(Container, sMODULE_NAME, sACCESS_TYPE, String.Empty);
		}

		/// <summary>
		/// Returns the effective record-level ACL access for a DataRow, combining:
		///   1. User-level module access (GetUserAccess)
		///   2. Ownership check when nACLACCESS == ACL_ACCESS.OWNER
		///   3. Record-level security field (RECORD_LEVEL_SECURITY_XXX) when enabled for the module
		/// BEFORE: Application["Modules.X.RecordLevelSecurity"]
		/// AFTER:  _memoryCache.Get("Modules.X.RecordLevelSecurity")
		/// </summary>
		public int GetRecordAccess(DataRow row, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_FIELD)
		{
			// 11/03/2017 Paul.  Remove is the same as edit.  We don't want to define another select field.
			if (sACCESS_TYPE == "remove")
				sACCESS_TYPE = "edit";
			int nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			if (row != null)
			{
				// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
				bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_assignment"));
				if (nACLACCESS == ACL_ACCESS.OWNER)
				{
					// 10/31/2017 Paul.  Don't check if sASSIGNED_USER_ID_FIELD exists — coding error that we want to catch.
					if (!Sql.IsEmptyString(sASSIGNED_USER_ID_FIELD))
					{
						// 01/24/2018 Paul.  sASSIGNED_USER_ID_FIELD is either ASSIGNED_USER_ID or CREATED_BY_ID.
						string sASSIGNED_SET_LIST_FIELD = "ASSIGNED_SET_LIST";
						if (bEnableDynamicAssignment && (sASSIGNED_USER_ID_FIELD == "ASSIGNED_USER_ID") && row.Table.Columns.Contains(sASSIGNED_SET_LIST_FIELD))
						{
							string sASSIGNED_SET_LIST = Sql.ToString(row[sASSIGNED_SET_LIST_FIELD]).ToUpper();
							if (!sASSIGNED_SET_LIST.Contains(this.USER_ID.ToString().ToUpper()) && !Sql.IsEmptyString(sASSIGNED_SET_LIST))
								nACLACCESS = ACL_ACCESS.NONE;
						}
						else
						{
							Guid gASSIGNED_USER_ID = Sql.ToGuid(row[sASSIGNED_USER_ID_FIELD]);
							if (this.USER_ID != gASSIGNED_USER_ID && gASSIGNED_USER_ID != Guid.Empty)
								nACLACCESS = ACL_ACCESS.NONE;
						}
					}
				}
				// 11/01/2017 Paul.  Use a module-based flag so that Record Level Security is only enabled when needed.
				// BEFORE: Application["Modules." + sMODULE_NAME + ".RecordLevelSecurity"]
				// AFTER:  _memoryCache.Get("Modules." + sMODULE_NAME + ".RecordLevelSecurity")
				if (Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".RecordLevelSecurity")))
				{
					// 10/31/2017 Paul.  FULL_ACCESS means this is an Admin and Record ACL does not apply.
					if (nACLACCESS >= 0 && nACLACCESS < ACL_ACCESS.FULL_ACCESS)
					{
						string sRECORD_ACL_FIELD_NAME = "RECORD_LEVEL_SECURITY_" + sACCESS_TYPE.ToUpper();
						// 10/31/2017 Paul.  Check if field exists because it is dynamically injected.
						if (row.Table.Columns.Contains(sRECORD_ACL_FIELD_NAME))
						{
							// 10/31/2017 Paul.  Record ACL only applies if it takes away rights.
							int nRECORD_ACLACCESS = Sql.ToInteger(row[sRECORD_ACL_FIELD_NAME]);
							if (nRECORD_ACLACCESS < nACLACCESS)
								nACLACCESS = nRECORD_ACLACCESS;
						}
					}
				}
			}
			return nACLACCESS;
		}

		/// <inheritdoc cref="GetRecordAccess(DataRow, string, string, string)"/>
		public int GetRecordAccess(DataRow row, string sMODULE_NAME, string sACCESS_TYPE)
		{
			return GetRecordAccess(row, sMODULE_NAME, sACCESS_TYPE, String.Empty);
		}

		// =====================================================================================
		// Admin User Access
		// BEFORE: Application["CONFIG.allow_admin_roles"]
		// AFTER:  _memoryCache.Get("CONFIG.allow_admin_roles")
		// =====================================================================================

		// 03/15/2010 Paul.  New AdminUserAccess functions include Admin Delegation.
		/// <summary>
		/// Returns the effective ACL access level for admin or admin-delegate users.
		/// Admins always get ACL_ACCESS.ALL; delegates get user-specific access when admin roles are enabled.
		/// BEFORE: Application["CONFIG.allow_admin_roles"]
		/// AFTER:  _memoryCache.Get("CONFIG.allow_admin_roles")
		/// </summary>
		public int AdminUserAccess(string sMODULE_NAME, string sACCESS_TYPE)
		{
			if (this.IS_ADMIN)
				return ACL_ACCESS.ALL;
			int nACLACCESS = ACL_ACCESS.NONE;
			// BEFORE: Application["CONFIG.allow_admin_roles"]
			// AFTER:  _memoryCache.Get("CONFIG.allow_admin_roles")
			bool bAllowAdminRoles = Sql.ToBoolean(_memoryCache.Get("CONFIG.allow_admin_roles"));
			if (bAllowAdminRoles)
			{
				if (this.IS_ADMIN_DELEGATE)
				{
					nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				}
			}
			return nACLACCESS;
		}

		/// <summary>
		/// Admin access check with an ownership qualifier.
		/// Returns NONE if access is OWNER and the record is not assigned to the current user.
		/// </summary>
		public int AdminUserAccess(string sMODULE_NAME, string sACCESS_TYPE, Guid gASSIGNED_USER_ID)
		{
			int nACLACCESS = this.AdminUserAccess(sMODULE_NAME, sACCESS_TYPE);
			if (nACLACCESS == ACL_ACCESS.OWNER && this.USER_ID != gASSIGNED_USER_ID && gASSIGNED_USER_ID != Guid.Empty)
			{
				nACLACCESS = ACL_ACCESS.NONE;
			}
			return nACLACCESS;
		}

		// =====================================================================================
		// Field-Level Security
		// BEFORE: Session["ACLFIELD_MODULE_FIELD"] = nACLACCESS  (stored as int in InProc)
		// AFTER:  Session.SetString("ACLFIELD_MODULE_FIELD", nACLACCESS.ToString())
		// =====================================================================================

		// 01/17/2010 Paul.  Field Security values are stored in the Session cache.
		/// <summary>
		/// Stores a field-level ACL access value in the session.
		/// Zero (NOT_SET) is excluded from storage — zero means "inherit full access".
		/// </summary>
		public void SetUserFieldSecurity(string sMODULE_NAME, string sFIELD_NAME, int nACLACCESS)
		{
			if (Session == null)
				throw new Exception("HttpContext.Current.Session is null");
			// 06/04/2006 Paul.  Verify that sMODULE_NAME is not empty.
			if (Sql.IsEmptyString(sMODULE_NAME))
				throw new Exception("SetUserFieldSecurity: sMODULE_NAME should not be empty.");
			if (Sql.IsEmptyString(sFIELD_NAME))
				throw new Exception("SetUserFieldSecurity: sFIELD_NAME should not be empty.");
			// 01/17/2010 Paul.  Zero is a special value that means NOT_SET.
			if (nACLACCESS != 0)
				Session.SetString("ACLFIELD_" + sMODULE_NAME + "_" + sFIELD_NAME, nACLACCESS.ToString());
		}

		/// <summary>
		/// Internal field-level security lookup — returns raw ACL integer from session.
		/// Zero (NOT_SET) maps to FULL_ACCESS for this user.
		/// </summary>
		protected int GetUserFieldSecurityInternal(string sMODULE_NAME, string sFIELD_NAME)
		{
			if (Session == null)
				throw new Exception("HttpContext.Current.Session is null");
			if (Sql.IsEmptyString(sMODULE_NAME))
				throw new Exception("GetUserFieldSecurity: sMODULE_NAME should not be empty.");
#if !DEBUG
			// 01/18/2010 Paul.  Disable Admin access in a debug build so that we can test the logic.
			if (IS_ADMIN)
				return ACL_FIELD_ACCESS.FULL_ACCESS;
#endif
			string sAclKey = "ACLFIELD_" + sMODULE_NAME + "_" + sFIELD_NAME;
			int nACLACCESS = Sql.ToInteger(Session.GetString(sAclKey));
			// 01/17/2010 Paul.  Zero is a special value that means NOT_SET, so grant full access.
			if (nACLACCESS == 0)
				return ACL_FIELD_ACCESS.FULL_ACCESS;
			return nACLACCESS;
		}

		/// <summary>
		/// Returns a typed ACL_FIELD_ACCESS object combining the field's access level with
		/// the owner context (whether the current user owns the record).
		/// </summary>
		public ACL_FIELD_ACCESS GetUserFieldSecurity(string sMODULE_NAME, string sFIELD_NAME, Guid gASSIGNED_USER_ID)
		{
			int nACLACCESS = GetUserFieldSecurityInternal(sMODULE_NAME, sFIELD_NAME);
			// Pass USER_ID explicitly to break circular dependency (ACL_FIELD_ACCESS is standalone, not DI)
			return new ACL_FIELD_ACCESS(nACLACCESS, gASSIGNED_USER_ID, this.USER_ID);
		}

		// =====================================================================================
		// Team Hierarchy Saved Search
		// BEFORE: DataTable dt = SplendidCache.SavedSearch("TeamHierarchy")   (static, circular dep)
		// AFTER:  Read JSON-serialised DataTable directly from session under the same key
		// =====================================================================================

		// 01/05/2020 Paul.  Provide central location for constant.
		/// <summary>Module name constant used by Filter() to identify the team hierarchy saved search.</summary>
		public const string TeamHierarchyModule = "TeamHierarchy";

		// 02/23/2017 Paul.  Add support for Team Hierarchy.
		/// <summary>
		/// Reads the saved team hierarchy search (if any) from the current session and populates
		/// gTEAM_ID and sTEAM_NAME with the selected hierarchy root.
		/// 
		/// BEFORE: Called SplendidCache.SavedSearch("TeamHierarchy") — static method on DI service
		///         that would create a circular dependency (SplendidCache depends on Security).
		/// AFTER:  Reads directly from session under key "vwSAVED_SEARCH.TeamHierarchy".
		///         SplendidCache stores the DataTable as a JSON string when writing to distributed session.
		///         XmlUtil.SelectSingleNode() is called statically — no DI required.
		/// </summary>
		public void TeamHierarchySavedSearch(ref Guid gTEAM_ID, ref string sTEAM_NAME)
		{
			// 01/05/2020 Paul.  Provide central location for constant.
			string sSEARCH_MODULE = Security.TeamHierarchyModule;

			// BEFORE: DataTable dt = SplendidCache.SavedSearch(sSEARCH_MODULE)
			// AFTER:  Deserialise from session JSON to avoid circular SplendidCache → Security dependency
			string sJson = Session?.GetString("vwSAVED_SEARCH." + sSEARCH_MODULE);
			if (Sql.IsEmptyString(sJson))
				return;

			DataTable dt = null;
			try
			{
				dt = JsonConvert.DeserializeObject<DataTable>(sJson);
			}
			catch
			{
				// Malformed JSON — ignore and leave gTEAM_ID/sTEAM_NAME as default empty values
				return;
			}

			if (dt != null && dt.Rows.Count > 0)
			{
				DataRow row = dt.Rows[0];
				string sXML = Sql.ToString(row["CONTENTS"]);
				XmlDocument xml = new XmlDocument();
				xml.LoadXml(sXML);
				sTEAM_NAME = Sql.ToString(XmlUtil.SelectSingleNode(xml.DocumentElement, "SearchFields/Field[@Name='NAME']"));
				gTEAM_ID   = Sql.ToGuid  (XmlUtil.SelectSingleNode(xml.DocumentElement, "SearchFields/Field[@Name='ID'  ]"));
			}
		}

		// =====================================================================================
		// Filter() — SQL WHERE clause + parameter injection for ACL enforcement
		//
		// These methods APPEND SQL JOIN clauses and WHERE conditions directly to cmd.CommandText
		// and bind Guid parameters via Sql.AddParameter().  The generated SQL predicates must be
		// byte-identical to the .NET Framework 4.8 output — they are used by every list view query.
		//
		// BEFORE: Crm.Config.enable_team_management()  → Application["CONFIG.enable_team_management"]
		// AFTER:  _memoryCache.Get("CONFIG.enable_team_management")  (same cache key family)
		//
		// BEFORE: Application["Modules.X.Teamed"]  / Application["Modules.X.Assigned"]
		// AFTER:  _memoryCache.Get("Modules.X.Teamed") / _memoryCache.Get("Modules.X.Assigned")
		//
		// Private helpers NextPlaceholder(), MetadataName(), HexEncode() are inlined here because
		// the migrated Sql.cs does not yet expose these methods as public statics.
		// =====================================================================================

		/// <summary>Filter overload that defaults to ASSIGNED_USER_ID field.</summary>
		public void Filter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE)
		{
			Filter(cmd, sMODULE_NAME, sACCESS_TYPE, "ASSIGNED_USER_ID");
		}

		// 04/24/2018 Paul.  Provide a way to exclude the SavedSearch for areas that are global in nature.
		/// <summary>Filter overload that defaults bExcludeSavedSearch=false.</summary>
		public void Filter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field)
		{
			Filter(cmd, sMODULE_NAME, sACCESS_TYPE, sASSIGNED_USER_ID_Field, false);
		}

		// 08/30/2009 Paul.  We need to know if this is an activities filter so that we can use the special activities teams view.
		// 06/02/2016 Paul.  Activities views will use new function that accepts an array of modules.
		// 04/24/2018 Paul.  Provide a way to exclude the SavedSearch for areas that are global in nature.
		/// <summary>
		/// Core single-module Filter method.  Appends team-membership JOINs and user-assignment WHERE
		/// predicates to cmd.CommandText, then binds USER_ID as a parameter.
		/// Produces identical SQL to the legacy .NET Framework 4.8 version.
		/// </summary>
		public void Filter(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field, bool bExcludeSavedSearch)
		{
			// 08/04/2007 Paul.  Always wait forever for the data.  No sense in showing a timeout.
			cmd.CommandTimeout = 0;
			// 01/22/2007 Paul.  If ASSIGNED_USER_ID is null, let everybody see it (workaround for auto-assignment bug).
			// BEFORE: Crm.Config.show_unassigned() → Application["CONFIG.show_unassigned"]
			// AFTER:  _memoryCache.Get("CONFIG.show_unassigned")
			bool bShowUnassigned        = Sql.ToBoolean(_memoryCache.Get("CONFIG.show_unassigned"));
			// 12/07/2006 Paul.  Not all views use ASSIGNED_USER_ID as the assigned field.  Allow an override.
			// 11/25/2006 Paul.  Administrators should not be restricted from seeing items because of team rights.
			// BEFORE: Application["Modules.X.Teamed"] / Application["Modules.X.Assigned"]
			// AFTER:  _memoryCache.Get("Modules.X.Teamed") / _memoryCache.Get("Modules.X.Assigned")
			bool bModuleIsTeamed        = Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".Teamed"  ));
			bool bModuleIsAssigned      = Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".Assigned"));
			// BEFORE: Crm.Config.enable_team_management() → Application["CONFIG.enable_team_management"] || Application["CONFIG.enable_multi_tenant_teams"]
			// AFTER:  _memoryCache.Get("CONFIG.enable_team_management") || _memoryCache.Get("CONFIG.enable_multi_tenant_teams")
			bool bEnableTeamManagement  = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_team_management" )) || Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_multi_tenant_teams"));
			bool bRequireTeamManagement = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_team_management")) || Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_multi_tenant_teams"));
			bool bRequireUserAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_user_assignment"));
			// 08/28/2009 Paul.  Allow dynamic teams to be turned off.
			bool bEnableDynamicTeams    = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_teams"   ));
			// 04/28/2016 Paul.  Allow team hierarchy.
			bool bEnableTeamHierarchy   = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_team_hierarchy"  ));
			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_assignment"));
			bool bIsAdmin = IS_ADMIN;
			// 08/30/2009 Paul.  Don't apply admin rules when debugging so that we can test the code.
#if DEBUG
			bIsAdmin = false;
#endif
			// 06/26/2018 Paul.  The Data Privacy Manager has admin-like access to Accounts, Contacts, Leads and Prospects.
			if (this.GetACLRoleAccess("Data Privacy Manager Role"))
			{
				if (sMODULE_NAME == "Accounts" || sMODULE_NAME == "Contacts" || sMODULE_NAME == "Leads" || sMODULE_NAME == "Prospects")
				{
					bIsAdmin = true;
				}
			}
			if (bModuleIsTeamed)
			{
				if (bIsAdmin)
					bRequireTeamManagement = false;

				if (bEnableTeamManagement)
				{
					// 11/12/2009 Paul.  Use NextPlaceholder so that we can call the security filter multiple times.
					string sFieldPlaceholder = NextPlaceholder(cmd, "MEMBERSHIP_USER_ID");
					if (bEnableDynamicTeams)
					{
						// 08/31/2009 Paul.  Dynamic Teams use a different view but same JOIN pattern.
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						// 04/28/2016 Paul.  Allow team hierarchy.
						if (!bEnableTeamHierarchy)
						{
							// 11/27/2009 Paul.  vwTEAM_SET_MEMBERSHIPS_Security has a distinct clause to reduce duplicate rows.
							cmd.CommandText += "join " + MetadataName(cmd, "vwTEAM_SET_MEMBERSHIPS_Security") + " vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_USER_ID     = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(" + MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ")) vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ") vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
						}
					}
					else
					{
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						// 04/28/2016 Paul.  Allow team hierarchy.
						if (!bEnableTeamHierarchy)
						{
							cmd.CommandText += "join vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_MEMBERSHIPS.MEMBERSHIP_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ")) vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + "fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ") vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
						}
					}
					Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
					// 02/23/2017 Paul.  Add support for Team Hierarchy.
					// 06/05/2017 Paul.  The SavedSearch does not apply to the Dashboard.
					// 04/24/2018 Paul.  Provide a way to exclude the SavedSearch for areas that are global in nature.
					if (bEnableTeamHierarchy && sMODULE_NAME != "Dashboard" && !bExcludeSavedSearch)
					{
						// 02/25/2017 Paul.  Using an inner join is much faster than using TEAM_ID in (select ID from ...).
						Guid   gTEAM_ID   = Guid.Empty;
						string sTEAM_NAME = String.Empty;
						this.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
						if (!Sql.IsEmptyGuid(gTEAM_ID))
						{
							string sFieldPlaceholder2 = NextPlaceholder(cmd, "TEAM_ID");
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "       inner join table(fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ")) vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "       inner join " + fnPrefix + "fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ") vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							Sql.AddParameter(cmd, sFieldPlaceholder2, gTEAM_ID);
						}
					}
				}
			}
			int nACLACCESS = 0;
			if (bModuleIsAssigned && !Sql.IsEmptyString(sMODULE_NAME))
			{
				nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			}

			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			string sASSIGNED_SET_ID_Field = sASSIGNED_USER_ID_Field.Replace("ASSIGNED_USER_ID", "ASSIGNED_SET_ID");
			if (bModuleIsAssigned && bEnableDynamicAssignment)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_SET_ID_Field);
					if (bRequireUserAssignment && !bShowUnassigned)
						cmd.CommandText += "       inner ";
					else
						cmd.CommandText += "  left outer ";
					cmd.CommandText += "join vwASSIGNED_SET_MEMBERSHIPS" + ControlChars.CrLf;
					cmd.CommandText += "               on vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID  = " + sASSIGNED_SET_ID_Field + ControlChars.CrLf;
					cmd.CommandText += "              and vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
					Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
				}
			}

			cmd.CommandText += " where 1 = 1" + ControlChars.CrLf;
			if (bModuleIsTeamed)
			{
				if (bEnableTeamManagement && !bRequireTeamManagement && !bIsAdmin)
				{
					// 08/31/2009 Paul.  Dynamic Teams handled just like regular teams, different view.
					// 09/01/2009 Paul.  Don't use MEMBERSHIP_ID as it is not included in the index.
					if (bEnableDynamicTeams)
						cmd.CommandText += "   and (TEAM_SET_ID is null or vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID is not null)" + ControlChars.CrLf;
					else
						cmd.CommandText += "   and (TEAM_ID is null or vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID is not null)" + ControlChars.CrLf;
				}
			}
			if (bModuleIsAssigned)
			{
				// 01/01/2008 Paul.  We need a quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
					if (bEnableDynamicAssignment)
					{
						if (bShowUnassigned)
						{
							cmd.CommandText += "   and (" + sASSIGNED_SET_ID_Field + " is null or vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID is not null)" + ControlChars.CrLf;
						}
					}
					else
					{
						string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_USER_ID_Field);
						if (bShowUnassigned)
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + "))" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder + ")"  + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder       + ControlChars.CrLf;
						}
						Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
					}
				}
			}
		}

		// 12/03/2017 Paul.  Module name field needs to be a parameter because it can change between MODULE_NAME and ACTIVITY_TYPE.
		/// <summary>
		/// Multi-module Filter for Activities/Stream views.  Appends team-membership JOINs and
		/// per-module OR assignment clauses to cmd.CommandText, then binds parameters.
		/// Produces identical SQL to the legacy .NET Framework 4.8 version.
		/// </summary>
		public void Filter(IDbCommand cmd, string[] arrModules, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field, string sMODULE_NAME_Field)
		{
			cmd.CommandTimeout = 0;
			// 01/22/2007 Paul.  If ASSIGNED_USER_ID is null, let everybody see it.
			bool bShowUnassigned        = Sql.ToBoolean(_memoryCache.Get("CONFIG.show_unassigned"));
			// 06/02/2016 Paul.  Stream and Activity tables are all teamed and assigned.
			bool bModuleIsTeamed        = true;
			bool bModuleIsAssigned      = true;
			bool bEnableTeamManagement  = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_team_management" )) || Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_multi_tenant_teams"));
			bool bRequireTeamManagement = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_team_management")) || Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_multi_tenant_teams"));
			bool bRequireUserAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_user_assignment"));
			bool bEnableDynamicTeams    = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_teams"  ));
			bool bEnableTeamHierarchy   = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_team_hierarchy" ));
			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_assignment"));
			bool bIsAdmin = IS_ADMIN;
#if DEBUG
			bIsAdmin = false;
#endif
			if (bModuleIsTeamed)
			{
				if (bIsAdmin)
					bRequireTeamManagement = false;

				if (bEnableTeamManagement)
				{
					string sFieldPlaceholder = NextPlaceholder(cmd, "MEMBERSHIP_USER_ID");
					if (bEnableDynamicTeams)
					{
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						if (!bEnableTeamHierarchy)
						{
							cmd.CommandText += "join " + MetadataName(cmd, "vwTEAM_SET_MEMBERSHIPS_Security") + " vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_USER_ID     = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(" + MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ")) vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + MetadataName(cmd, "fnTEAM_SET_HIERARCHY_MEMBERSHIPS") + "(@" + sFieldPlaceholder + ") vwTEAM_SET_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID = TEAM_SET_ID" + ControlChars.CrLf;
							}
						}
					}
					else
					{
						if (bRequireTeamManagement)
							cmd.CommandText += "       inner ";
						else
							cmd.CommandText += "  left outer ";
						if (!bEnableTeamHierarchy)
						{
							cmd.CommandText += "join vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
							cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							cmd.CommandText += "              and vwTEAM_MEMBERSHIPS.MEMBERSHIP_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "join table(fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ")) vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "join " + fnPrefix + "fnTEAM_HIERARCHY_MEMBERSHIPS(@" + sFieldPlaceholder + ") vwTEAM_MEMBERSHIPS" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
						}
					}
					Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
					// 02/23/2017 Paul.  Add support for Team Hierarchy.
					if (bEnableTeamHierarchy)
					{
						// 02/25/2017 Paul.  Using an inner join is much faster than TEAM_ID in (select ID from ...).
						Guid   gTEAM_ID   = Guid.Empty;
						string sTEAM_NAME = String.Empty;
						this.TeamHierarchySavedSearch(ref gTEAM_ID, ref sTEAM_NAME);
						if (!Sql.IsEmptyGuid(gTEAM_ID))
						{
							string sFieldPlaceholder2 = NextPlaceholder(cmd, "TEAM_ID");
							if (Sql.IsOracle(cmd))
							{
								cmd.CommandText += "       inner join table(fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ")) vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							else
							{
								string fnPrefix = (Sql.IsSQLServer(cmd) ? "dbo." : String.Empty);
								cmd.CommandText += "       inner join " + fnPrefix + "fnTEAM_HIERARCHY_ByTeam(@" + sFieldPlaceholder2 + ") vwTEAM_HIERARCHY_ByTeam" + ControlChars.CrLf;
								cmd.CommandText += "               on vwTEAM_HIERARCHY_ByTeam.MEMBERSHIP_TEAM_ID = TEAM_ID" + ControlChars.CrLf;
							}
							Sql.AddParameter(cmd, sFieldPlaceholder2, gTEAM_ID);
						}
					}
				}
			}
			// 06/02/2016 Paul.  We need to first determine if the assignment rules should be applied.
			bool bApplyAssignmentRules = false;
			foreach (string sMODULE_NAME in arrModules)
			{
				int nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					bApplyAssignmentRules = true;
				}
			}
			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			string sASSIGNED_SET_ID_Field = sASSIGNED_USER_ID_Field.Replace("ASSIGNED_USER_ID", "ASSIGNED_SET_ID");
			if (bModuleIsAssigned && bApplyAssignmentRules && bEnableDynamicAssignment)
			{
				// 01/01/2008 Paul.  Quick way to require user assignments across the system.
				// 01/02/2008 Paul.  Make sure owner rule does not apply to admins.
				foreach (string sMODULE_NAME in arrModules)
				{
					int nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
					if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
					{
						string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_SET_ID_Field);
						// 12/03/2017 Paul.  We need to use an outer join because there would be one join per module.
						cmd.CommandText += "  left outer ";
						cmd.CommandText += "join vwASSIGNED_SET_MEMBERSHIPS   vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ControlChars.CrLf;
						cmd.CommandText += "               on vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_SET_ID  = " + sASSIGNED_SET_ID_Field + ControlChars.CrLf;
						cmd.CommandText += "              and vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
						Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
					}
				}
			}

			cmd.CommandText += " where 1 = 1" + ControlChars.CrLf;
			if (bModuleIsTeamed)
			{
				if (bEnableTeamManagement && !bRequireTeamManagement && !bIsAdmin)
				{
					if (bEnableDynamicTeams)
						cmd.CommandText += "   and (TEAM_SET_ID is null or vwTEAM_SET_MEMBERSHIPS.MEMBERSHIP_TEAM_SET_ID is not null)" + ControlChars.CrLf;
					else
						cmd.CommandText += "   and (TEAM_ID is null or vwTEAM_MEMBERSHIPS.MEMBERSHIP_TEAM_ID is not null)" + ControlChars.CrLf;
				}
			}
			if (bModuleIsAssigned && bApplyAssignmentRules)
			{
				cmd.CommandText += "   and ( 1 = 0" + ControlChars.CrLf;
				foreach (string sMODULE_NAME in arrModules)
				{
					// 12/03/2017 Paul.  Pad short module names so the SQL is aligned for readability.
					string sModuleSpacer = "";
					if (sMODULE_NAME.Length < 15)
						sModuleSpacer = Strings.Space(15 - sMODULE_NAME.Length);
					int nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
					if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
					{
						// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
						if (bEnableDynamicAssignment)
						{
							if (bShowUnassigned)
							{
								string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
								cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (" + sASSIGNED_SET_ID_Field + " is null or vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_SET_ID is not null))" + ControlChars.CrLf;
								Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME);
							}
							else
							{
								string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
								cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (vwASSIGNED_SET_MEMBERSHIPS_" + sMODULE_NAME + ".MEMBERSHIP_ASSIGNED_SET_ID is not null))" + ControlChars.CrLf;
								Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME);
							}
						}
						else
						{
							string sFieldPlaceholder  = NextPlaceholder(cmd, sASSIGNED_USER_ID_Field);
							string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
							if (bShowUnassigned)
							{
								if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (" + sASSIGNED_USER_ID_Field + " is null or upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")))" + ControlChars.CrLf;
								else
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and (" + sASSIGNED_USER_ID_Field + " is null or "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder +  "))" + ControlChars.CrLf;
							}
							else
							{
								if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + "))" + ControlChars.CrLf;
								else
									cmd.CommandText += "         or (" + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + sModuleSpacer + " and "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder +  ")" + ControlChars.CrLf;
							}
							Sql.AddParameter(cmd, sFieldPlaceholder , this.USER_ID  );
							Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME  );
						}
					}
					else if (nACLACCESS > 0)
					{
						string sMODULEPlaceholder = NextPlaceholder(cmd, sMODULE_NAME_Field);
						cmd.CommandText += "          or " + sMODULE_NAME_Field + " = @" + sMODULEPlaceholder + ControlChars.CrLf;
						Sql.AddParameter(cmd, sMODULEPlaceholder, sMODULE_NAME);
					}
				}
				cmd.CommandText += "       )" + ControlChars.CrLf;
			}
		}

		// 06/26/2018 Paul.  New filter for Data Privacy.  Only Assigned User applies, not team management.
		/// <summary>
		/// Assigned-only Filter — omits team management JOINs, enforcing only user-assignment constraints.
		/// Used for Data Privacy operations where only the assigned user may edit a record.
		/// Produces identical SQL predicates to the legacy .NET Framework 4.8 version.
		/// </summary>
		public void FilterAssigned(IDbCommand cmd, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field)
		{
			cmd.CommandTimeout = 0;
			bool bShowUnassigned          = Sql.ToBoolean(_memoryCache.Get("CONFIG.show_unassigned"));
			bool bModuleIsAssigned        = true;
			bool bRequireUserAssignment   = true;
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_assignment"));
			bool bIsAdmin = IS_ADMIN;
			// 06/26/2018 Paul.  The Data Privacy Manager has admin-like access to Accounts, Contacts, Leads and Prospects.
			if (this.GetACLRoleAccess("Data Privacy Manager Role"))
			{
				if (sMODULE_NAME == "Accounts" || sMODULE_NAME == "Contacts" || sMODULE_NAME == "Leads" || sMODULE_NAME == "Prospects")
				{
					bIsAdmin = true;
				}
			}
			int nACLACCESS = 0;
			if (bModuleIsAssigned && !Sql.IsEmptyString(sMODULE_NAME))
			{
				nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			}

			// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
			string sASSIGNED_SET_ID_Field = sASSIGNED_USER_ID_Field.Replace("ASSIGNED_USER_ID", "ASSIGNED_SET_ID");
			if (bModuleIsAssigned && bEnableDynamicAssignment)
			{
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_SET_ID_Field);
					if (bRequireUserAssignment && !bShowUnassigned)
						cmd.CommandText += "       inner ";
					else
						cmd.CommandText += "  left outer ";
					cmd.CommandText += "join vwASSIGNED_SET_MEMBERSHIPS" + ControlChars.CrLf;
					cmd.CommandText += "               on vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID  = " + sASSIGNED_SET_ID_Field + ControlChars.CrLf;
					cmd.CommandText += "              and vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_USER_ID = @" + sFieldPlaceholder + ControlChars.CrLf;
					Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
				}
			}

			cmd.CommandText += " where 1 = 1" + ControlChars.CrLf;
			if (bModuleIsAssigned)
			{
				if (nACLACCESS == ACL_ACCESS.OWNER || (bRequireUserAssignment && !bIsAdmin))
				{
					// 11/30/2017 Paul.  Add ASSIGNED_SET_ID for Dynamic User Assignment.
					if (bEnableDynamicAssignment)
					{
						if (bShowUnassigned)
						{
							cmd.CommandText += "   and (" + sASSIGNED_SET_ID_Field + " is null or vwASSIGNED_SET_MEMBERSHIPS.MEMBERSHIP_ASSIGNED_SET_ID is not null)" + ControlChars.CrLf;
						}
					}
					else
					{
						string sFieldPlaceholder = NextPlaceholder(cmd, sASSIGNED_USER_ID_Field);
						if (bShowUnassigned)
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + "))" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and (" + sASSIGNED_USER_ID_Field + " is null or "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder + ")"  + ControlChars.CrLf;
						}
						else
						{
							if (Sql.IsOracle(cmd) || Sql.IsDB2(cmd))
								cmd.CommandText += "   and upper(" + sASSIGNED_USER_ID_Field + ") = upper(@" + sFieldPlaceholder + ")" + ControlChars.CrLf;
							else
								cmd.CommandText += "   and "       + sASSIGNED_USER_ID_Field +  " = @"       + sFieldPlaceholder       + ControlChars.CrLf;
						}
						Sql.AddParameter(cmd, sFieldPlaceholder, this.USER_ID);
					}
				}
			}
		}

		// =====================================================================================
		// String-based Filter convenience overloads
		// These variants return SQL string fragments (not parameterized) suitable for REST
		// controller queries that build SQL strings rather than IDbCommand objects.
		// Used by RestController.cs and AdminRestController.cs.
		//
		// NOTE: GUID values are formatted as UUID literals which are safe against SQL injection
		// since Guid is a strongly-typed struct with a fixed hex format.
		// =====================================================================================

		/// <summary>
		/// Returns a SQL WHERE clause fragment string (without the leading "where 1=1") that
		/// enforces user-assignment ACL for the given module and access type.
		/// Suitable for inline string concatenation into a query that already has "where 1 = 1".
		/// </summary>
		/// <param name="gUSER_ID">The current user ID (Security.USER_ID) — used for ASSIGNED_USER_ID comparison.</param>
		/// <param name="sMODULE_NAME">Module to evaluate ACL for.</param>
		/// <param name="sACCESS_TYPE">Access type: "list", "view", "edit", "delete", "export", "import".</param>
		/// <returns>SQL fragment e.g. " and ASSIGNED_USER_ID = 'guid'" or "" for admins.</returns>
		public string Filter(Guid gUSER_ID, string sMODULE_NAME, string sACCESS_TYPE)
		{
			bool bModuleIsAssigned    = Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".Assigned"));
			bool bRequireUserAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_user_assignment"));
			bool bShowUnassigned      = Sql.ToBoolean(_memoryCache.Get("CONFIG.show_unassigned"));
			bool bEnableDynamicAssignment = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_assignment"));
			bool bIsAdmin = IS_ADMIN;
#if DEBUG
			bIsAdmin = false;
#endif
			int nACLACCESS = 0;
			if (bModuleIsAssigned && !Sql.IsEmptyString(sMODULE_NAME))
				nACLACCESS = this.GetUserAccess(sMODULE_NAME, sACCESS_TYPE);

			if (!bModuleIsAssigned)
				return String.Empty;
			if (bIsAdmin)
				return String.Empty;
			if (nACLACCESS != ACL_ACCESS.OWNER && !(bRequireUserAssignment && !bIsAdmin))
				return String.Empty;

			// ASSIGNED_SET_ID for Dynamic User Assignment
			if (bEnableDynamicAssignment)
			{
				if (bShowUnassigned)
					return " and (ASSIGNED_SET_ID is null or ASSIGNED_SET_ID in (select ASSIGNED_SET_ID from vwASSIGNED_SET_MEMBERSHIPS where MEMBERSHIP_ASSIGNED_USER_ID = '" + gUSER_ID.ToString() + "'))";
				else
					return " and ASSIGNED_SET_ID in (select ASSIGNED_SET_ID from vwASSIGNED_SET_MEMBERSHIPS where MEMBERSHIP_ASSIGNED_USER_ID = '" + gUSER_ID.ToString() + "')";
			}

			if (bShowUnassigned)
				return " and (ASSIGNED_USER_ID is null or ASSIGNED_USER_ID = '" + gUSER_ID.ToString() + "')";
			else
				return " and ASSIGNED_USER_ID = '" + gUSER_ID.ToString() + "'";
		}

		/// <summary>
		/// Returns a SQL WHERE clause fragment string enforcing team membership for the given module.
		/// Used when team management is enabled and the module is team-aware.
		/// Suitable for inline string concatenation into a query that already has "where 1 = 1".
		/// </summary>
		/// <param name="sMODULE_NAME">Module to evaluate team membership for.</param>
		/// <returns>SQL fragment for team membership filter, or "" if team management is off or user is admin.</returns>
		public string FilterByTeam(string sMODULE_NAME)
		{
			bool bModuleIsTeamed      = Sql.ToBoolean(_memoryCache.Get("Modules." + sMODULE_NAME + ".Teamed"));
			bool bEnableTeamManagement  = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_team_management" )) || Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_multi_tenant_teams"));
			bool bRequireTeamManagement = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_team_management")) || Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_multi_tenant_teams"));
			bool bEnableDynamicTeams  = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_dynamic_teams"    ));
			bool bIsAdmin = IS_ADMIN;
#if DEBUG
			bIsAdmin = false;
#endif
			if (!bModuleIsTeamed || !bEnableTeamManagement || bIsAdmin)
				return String.Empty;

			Guid gUSER_ID = this.USER_ID;
			if (bEnableDynamicTeams)
			{
				if (bRequireTeamManagement)
					return " and TEAM_SET_ID in (select MEMBERSHIP_TEAM_SET_ID from vwTEAM_SET_MEMBERSHIPS_Security where MEMBERSHIP_USER_ID = '" + gUSER_ID.ToString() + "')";
				else
					return " and (TEAM_SET_ID is null or TEAM_SET_ID in (select MEMBERSHIP_TEAM_SET_ID from vwTEAM_SET_MEMBERSHIPS_Security where MEMBERSHIP_USER_ID = '" + gUSER_ID.ToString() + "'))";
			}
			else
			{
				if (bRequireTeamManagement)
					return " and TEAM_ID in (select MEMBERSHIP_TEAM_ID from vwTEAM_MEMBERSHIPS where MEMBERSHIP_USER_ID = '" + gUSER_ID.ToString() + "')";
				else
					return " and (TEAM_ID is null or TEAM_ID in (select MEMBERSHIP_TEAM_ID from vwTEAM_MEMBERSHIPS where MEMBERSHIP_USER_ID = '" + gUSER_ID.ToString() + "'))";
			}
		}

		// =====================================================================================
		// Private helper methods — inlined from Sql.cs (methods not yet exposed as public statics
		// in the migrated Sql.cs).  These must remain private to Security.cs.
		// =====================================================================================

		/// <summary>
		/// Returns a unique parameter placeholder name for the given field, incrementing a suffix
		/// integer if a parameter with the base name already exists on the command.
		/// Inlined from Sql.NextPlaceholder(IDbCommand, string) in the legacy codebase.
		/// </summary>
		private static string NextPlaceholder(IDbCommand cmd, string sField)
		{
			// 12/26/2006 Paul.  We need to determine the next available placeholder name.
			int    nPlaceholderIndex  = 0;
			string sFieldPlaceholder  = sField;
			while (HasParameter(cmd, sFieldPlaceholder))
			{
				nPlaceholderIndex++;
				sFieldPlaceholder = sField + nPlaceholderIndex.ToString();
			}
			return sFieldPlaceholder;
		}

		/// <summary>
		/// Returns true when cmd already has a parameter named "@<paramref name="sFieldPlaceholder"/>".
		/// Used exclusively by NextPlaceholder to generate unique names.
		/// </summary>
		private static bool HasParameter(IDbCommand cmd, string sFieldPlaceholder)
		{
			string sTarget = "@" + sFieldPlaceholder;
			foreach (IDataParameter p in cmd.Parameters)
			{
				if (String.Equals(p.ParameterName, sTarget, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Returns the database-appropriate metadata name (view/function name) for the given identifier.
		/// Oracle truncates to 30 chars and forces uppercase; DB2 forces uppercase; SQL Server/others unchanged.
		/// Inlined from Sql.MetadataName(IDbCommand, string) in the legacy codebase.
		/// </summary>
		private static string MetadataName(IDbCommand cmd, string sNAME)
		{
			// 09/02/2008 Paul.  Tables and field names in DB2/Oracle must be uppercase.
			// 11/27/2009 Paul.  Truncate Oracle names to 30 characters.
			if (Sql.IsOracle(cmd))
				return sNAME.ToUpper().Substring(0, Math.Min(sNAME.Length, 30));
			else if (Sql.IsDB2(cmd))
				return sNAME.ToUpper();
			// SQL Server and other databases are not typically case-significant.
			return sNAME;
		}

		/// <summary>
		/// Converts a byte array to a lowercase hexadecimal string.
		/// Used by HashPassword() to format the MD5 digest.
		/// Inlined from Sql.HexEncode(byte[]) in the legacy codebase.
		/// </summary>
		private static string HexEncode(byte[] aby)
		{
			const string hex = "0123456789abcdef";
			StringBuilder sb = new StringBuilder(aby.Length * 2);
			for (int i = 0; i < aby.Length; i++)
			{
				sb.Append(hex[(aby[i] & 0xf0) >> 4]);
				sb.Append(hex[ aby[i] & 0x0f       ]);
			}
			return sb.ToString();
		}

		// =====================================================================================
		// ACL_FIELD_ACCESS — Nested class for field-level access control
		// Migrated as-is from the original Security.cs nested class.
		// The constructor signature gains gCurrentUserId to avoid static Security.USER_ID access
		// (Security is no longer a static class in .NET 10 migration).
		// =====================================================================================

		// 01/17/2010 Paul.  Create the class in Security as ACLFieldGrid.cs is not distributed with all editions.
		/// <summary>
		/// Encapsulates a field's ACL access level in the context of the current user and record owner.
		/// Determines readability/writability based on the 8-tier field-level access constants.
		/// </summary>
		public class ACL_FIELD_ACCESS
		{
			// Field access level constants
			public const int FULL_ACCESS            = 100;
			public const int READ_WRITE             =  99;
			public const int READ_OWNER_WRITE       =  60;
			public const int READ_ONLY              =  50;
			public const int OWNER_READ_OWNER_WRITE =  40;
			public const int OWNER_READ_ONLY        =  30;
			public const int NOT_SET                =   0;
			public const int NONE                   = -99;

			/// <summary>The raw ACL access integer for this field (exposed per schema).</summary>
			public int nACLACCESS;
			/// <summary>The owner GUID of the record (exposed per schema).</summary>
			public Guid gOWNER_ID;

			protected bool bIsNew  ;
			protected bool bIsOwner;

			/// <summary>The raw ACL access level integer.</summary>
			public int ACLACCESS
			{
				get { return nACLACCESS; }
			}

			/// <summary>True when the record is new (owner GUID is empty).</summary>
			public bool IsNew
			{
				get { return bIsNew; }
			}

			/// <summary>True when the current user is the record owner (or record is new).</summary>
			public bool IsOwner
			{
				get { return bIsOwner; }
			}

			/// <summary>
			/// Determines whether the current user can read this field.
			/// </summary>
			public bool IsReadable()
			{
				if (nACLACCESS == ACL_FIELD_ACCESS.FULL_ACCESS)
					return true;
				else if (nACLACCESS < ACL_FIELD_ACCESS.NOT_SET)
					return false;
				if (   bIsNew
				    || bIsOwner
				    || nACLACCESS > ACL_FIELD_ACCESS.OWNER_READ_ONLY
				   )
					return true;
				return false;
			}

			/// <summary>
			/// Determines whether the current user can write this field.
			/// </summary>
			public bool IsWriteable()
			{
				if (nACLACCESS == ACL_FIELD_ACCESS.FULL_ACCESS)
					return true;
				else if (nACLACCESS < ACL_FIELD_ACCESS.NOT_SET)
					return false;
				// 01/22/2010 Paul.  Just because the record is new, does not mean that the user can specify it.
				if (   (bIsOwner && nACLACCESS == ACL_FIELD_ACCESS.OWNER_READ_OWNER_WRITE)
				    || (bIsOwner && nACLACCESS == ACL_FIELD_ACCESS.READ_OWNER_WRITE      )
				    || (            nACLACCESS >  ACL_FIELD_ACCESS.READ_OWNER_WRITE      )
				   )
					return true;
				return false;
			}

			/// <summary>
			/// Constructs an ACL_FIELD_ACCESS for the given access level, record owner, and current user.
			/// BEFORE: public ACL_FIELD_ACCESS(int nACLACCESS, Guid gOWNER_ID) — referenced static Security.USER_ID
			/// AFTER:  gCurrentUserId passed explicitly to avoid dependency on static Security.USER_ID
			/// </summary>
			/// <param name="nACLACCESS">Raw ACL access integer from session.</param>
			/// <param name="gOWNER_ID">ASSIGNED_USER_ID or CREATED_BY_ID of the record.</param>
			/// <param name="gCurrentUserId">The authenticated user's ID (Security.USER_ID).</param>
			public ACL_FIELD_ACCESS(int nACLACCESS, Guid gOWNER_ID, Guid gCurrentUserId)
			{
				this.nACLACCESS = nACLACCESS;
				this.gOWNER_ID  = gOWNER_ID ;
				this.bIsNew     = (gOWNER_ID     == Guid.Empty      );
				this.bIsOwner   = (gCurrentUserId == gOWNER_ID) || bIsNew;
			}
		}
	}
}
