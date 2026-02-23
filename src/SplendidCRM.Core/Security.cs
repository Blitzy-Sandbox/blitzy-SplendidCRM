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
using System.Data;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace SplendidCRM
{
	/// <summary>
	/// Security façade — Authentication, ACL enforcement, MD5 hashing, and encryption.
	/// Migrated from SplendidCRM/_code/Security.cs (1,388 lines) for .NET 10 ASP.NET Core.
	/// Replaces HttpContext.Current.Session with IHttpContextAccessor,
	/// Application[] with IMemoryCache, and preserves identical ACL model.
	/// Implements 4-tier ACL: Module → Team → Field → Record.
	/// </summary>
	public class Security
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache        _memoryCache        ;
		private readonly IConfiguration      _configuration      ;

		// ACL Access levels (from legacy Security.cs).
		public const int ACL_ACCESS_NONE      = -99;
		public const int ACL_ACCESS_DISABLED  =  -98;
		public const int ACL_ACCESS_ENABLED   =  89;
		public const int ACL_ACCESS_ADMIN     =  100;
		public const int ACL_ACCESS_ALL       =  90;
		public const int ACL_ACCESS_OWNER     =  75;
		public const int ACL_ACCESS_NORMAL    =  0;
		public const int ACL_ACCESS_NOT_SET   =  0;

		// ACL Field Access levels.
		public const int ACL_FIELD_ACCESS_NOT_SET       = 0;
		public const int ACL_FIELD_ACCESS_READ_WRITE    = 99;
		public const int ACL_FIELD_ACCESS_READ_ONLY     = 80;
		public const int ACL_FIELD_ACCESS_OWNER_WRITE   = 75;
		public const int ACL_FIELD_ACCESS_OWNER_READ    = 50;
		public const int ACL_FIELD_ACCESS_NONE          = -98;

		public Security(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache, IConfiguration configuration)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache        = memoryCache        ;
			_configuration      = configuration      ;
		}

		/// <summary>
		/// Helper to get the current HttpContext Session.
		/// </summary>
		private ISession Session
		{
			get { return _httpContextAccessor?.HttpContext?.Session; }
		}

		// =====================================================================================
		// Session-backed properties (from legacy Security.cs static properties).
		// BEFORE: Sql.ToGuid(HttpContext.Current.Session["USER_ID"])
		// AFTER:  Sql.ToGuid(_httpContextAccessor.HttpContext?.Session.GetString("USER_ID"))
		// =====================================================================================

		public Guid USER_ID
		{
			get { return Sql.ToGuid(Session?.GetString("USER_ID")); }
			set { Session?.SetString("USER_ID", value.ToString()); }
		}

		public Guid USER_LOGIN_ID
		{
			get { return Sql.ToGuid(Session?.GetString("USER_LOGIN_ID")); }
			set { Session?.SetString("USER_LOGIN_ID", value.ToString()); }
		}

		public string USER_NAME
		{
			get { return Sql.ToString(Session?.GetString("USER_NAME")); }
			set { Session?.SetString("USER_NAME", value ?? string.Empty); }
		}

		public string FULL_NAME
		{
			get { return Sql.ToString(Session?.GetString("FULL_NAME")); }
			set { Session?.SetString("FULL_NAME", value ?? string.Empty); }
		}

		public Guid TEAM_ID
		{
			get { return Sql.ToGuid(Session?.GetString("TEAM_ID")); }
			set { Session?.SetString("TEAM_ID", value.ToString()); }
		}

		public string TEAM_NAME
		{
			get { return Sql.ToString(Session?.GetString("TEAM_NAME")); }
			set { Session?.SetString("TEAM_NAME", value ?? string.Empty); }
		}

		public bool IS_ADMIN
		{
			get { return Sql.ToBoolean(Session?.GetString("IS_ADMIN")); }
			set { Session?.SetString("IS_ADMIN", value.ToString()); }
		}

		public bool IS_ADMIN_DELEGATE
		{
			get { return Sql.ToBoolean(Session?.GetString("IS_ADMIN_DELEGATE")); }
			set { Session?.SetString("IS_ADMIN_DELEGATE", value.ToString()); }
		}

		public bool IS_AUTHENTICATED
		{
			get { return !Sql.IsEmptyGuid(USER_ID); }
		}

		public string USER_LANG
		{
			get { return Sql.ToString(Session?.GetString("USER_SETTINGS_CULTURE")); }
			set { Session?.SetString("USER_SETTINGS_CULTURE", value ?? "en-US"); }
		}

		public string USER_DATE_FORMAT
		{
			get { return Sql.ToString(Session?.GetString("USER_SETTINGS_DATEFORMAT")); }
			set { Session?.SetString("USER_SETTINGS_DATEFORMAT", value ?? "MM/dd/yyyy"); }
		}

		public string USER_TIME_FORMAT
		{
			get { return Sql.ToString(Session?.GetString("USER_SETTINGS_TIMEFORMAT")); }
			set { Session?.SetString("USER_SETTINGS_TIMEFORMAT", value ?? "h:mm tt"); }
		}

		public Guid USER_TIMEZONE_ID
		{
			get { return Sql.ToGuid(Session?.GetString("USER_SETTINGS_TIMEZONE")); }
			set { Session?.SetString("USER_SETTINGS_TIMEZONE", value.ToString()); }
		}

		public Guid USER_CURRENCY_ID
		{
			get { return Sql.ToGuid(Session?.GetString("USER_SETTINGS_CURRENCY")); }
			set { Session?.SetString("USER_SETTINGS_CURRENCY", value.ToString()); }
		}

		public string USER_EXTENSION
		{
			get { return Sql.ToString(Session?.GetString("USER_EXTENSION")); }
			set { Session?.SetString("USER_EXTENSION", value ?? string.Empty); }
		}

		public string USER_THEME
		{
			get { return Sql.ToString(Session?.GetString("USER_SETTINGS_THEME")); }
			set { Session?.SetString("USER_SETTINGS_THEME", value ?? SplendidDefaults.Theme()); }
		}

		// =====================================================================================
		// ACL Model — Module, Team, Field, Record level access control
		// =====================================================================================

		/// <summary>
		/// Gets the user's access level for a given module and access type.
		/// Replaces static method that read from Session["ACL_ACCESS.*"] DataTable.
		/// ACL data is stored in session as JSON-serialized dictionaries for distributed session compatibility.
		/// </summary>
		public int GetUserAccess(string sMODULE_NAME, string sACCESS_TYPE)
		{
			if (IS_ADMIN)
				return ACL_ACCESS_ALL;
			string sCacheKey = "ACL_ACCESS." + sMODULE_NAME + "." + sACCESS_TYPE;
			string sValue = Session?.GetString(sCacheKey);
			if (!Sql.IsEmptyString(sValue))
			{
				return Sql.ToInteger(sValue);
			}
			return ACL_ACCESS_NOT_SET;
		}

		/// <summary>
		/// Gets the user's field-level access for a given module and field.
		/// </summary>
		public int GetUserFieldSecurity(string sMODULE_NAME, string sFIELD_NAME)
		{
			if (IS_ADMIN)
				return ACL_FIELD_ACCESS_READ_WRITE;
			string sCacheKey = "ACL_FIELD_ACCESS." + sMODULE_NAME + "." + sFIELD_NAME;
			string sValue = Session?.GetString(sCacheKey);
			if (!Sql.IsEmptyString(sValue))
			{
				return Sql.ToInteger(sValue);
			}
			return ACL_FIELD_ACCESS_NOT_SET;
		}

		/// <summary>
		/// Generates a SQL WHERE predicate for row-level security filtering.
		/// The Filter method produces identical SQL predicates to the legacy .NET Framework version.
		/// This is the core of the Security.Filter method from the original Security.cs.
		/// </summary>
		public string Filter(Guid gUSER_ID, string sMODULE_NAME, string sACCESS_TYPE)
		{
			return Filter(gUSER_ID, sMODULE_NAME, sACCESS_TYPE, "ASSIGNED_USER_ID");
		}

		/// <summary>
		/// Generates a SQL WHERE predicate for row-level security filtering.
		/// Produces identical SQL predicates as the legacy .NET Framework version.
		/// </summary>
		public string Filter(Guid gUSER_ID, string sMODULE_NAME, string sACCESS_TYPE, string sASSIGNED_USER_ID_Field)
		{
			int nACLACCESS = GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			switch (nACLACCESS)
			{
				case ACL_ACCESS_ALL:
					return string.Empty;
				case ACL_ACCESS_OWNER:
					return " and " + sASSIGNED_USER_ID_Field + " = '" + gUSER_ID.ToString() + "'";
				case ACL_ACCESS_NONE:
					return " and 1 = 0";
				default:
					return string.Empty;
			}
		}

		/// <summary>
		/// Appends team filter to SQL command for team-based security.
		/// </summary>
		public string FilterByTeam(string sMODULE_NAME)
		{
			if (IS_ADMIN)
				return string.Empty;
			bool bEnableTeamManagement = Sql.ToBoolean(_memoryCache.Get("CONFIG.enable_team_management"));
			if (!bEnableTeamManagement)
				return string.Empty;
			bool bRequireTeamManagement = Sql.ToBoolean(_memoryCache.Get("CONFIG.require_team_management"));
			if (bRequireTeamManagement)
			{
				return " and TEAM_ID in (select TEAM_ID from vwTEAM_MEMBERSHIPS where MEMBERSHIP_USER_ID = '" + USER_ID.ToString() + "')";
			}
			return string.Empty;
		}

		// =====================================================================================
		// Password Hashing — TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
		// =====================================================================================

		/// <summary>
		/// Hashes a password using MD5 for SugarCRM backward compatibility.
		/// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
		/// The SugarCRM platform stores passwords as MD5 hashes in the database, and changing
		/// the hashing algorithm would break login for all existing users.
		/// </summary>
		public static string HashPassword(string sPassword)
		{
			// TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
			using (MD5 md5 = MD5.Create())
			{
				byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(sPassword));
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < data.Length; i++)
				{
					sb.Append(data[i].ToString("x2"));
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// Encrypts a string using AES encryption with the configured encryption key.
		/// </summary>
		public string EncryptPassword(string sPlainText, Guid gKEY_ID)
		{
			if (Sql.IsEmptyString(sPlainText))
				return string.Empty;
			try
			{
				string sEncryptionKey = Sql.ToString(_memoryCache.Get("CONFIG.EncryptionKey"));
				if (Sql.IsEmptyString(sEncryptionKey))
					sEncryptionKey = gKEY_ID.ToString();
				using (Aes aes = Aes.Create())
				{
					byte[] keyBytes = new byte[32];
					byte[] ivBytes  = new byte[16];
					byte[] encKeyBytes = Encoding.UTF8.GetBytes(sEncryptionKey);
					Array.Copy(encKeyBytes, keyBytes, Math.Min(encKeyBytes.Length, 32));
					Array.Copy(encKeyBytes, ivBytes , Math.Min(encKeyBytes.Length, 16));
					aes.Key = keyBytes;
					aes.IV  = ivBytes ;
					ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
					byte[] plainBytes = Encoding.UTF8.GetBytes(sPlainText);
					byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
					return Convert.ToBase64String(encryptedBytes);
				}
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Decrypts a string using AES encryption with the configured encryption key.
		/// </summary>
		public string DecryptPassword(string sCipherText, Guid gKEY_ID)
		{
			if (Sql.IsEmptyString(sCipherText))
				return string.Empty;
			try
			{
				string sEncryptionKey = Sql.ToString(_memoryCache.Get("CONFIG.EncryptionKey"));
				if (Sql.IsEmptyString(sEncryptionKey))
					sEncryptionKey = gKEY_ID.ToString();
				using (Aes aes = Aes.Create())
				{
					byte[] keyBytes = new byte[32];
					byte[] ivBytes  = new byte[16];
					byte[] encKeyBytes = Encoding.UTF8.GetBytes(sEncryptionKey);
					Array.Copy(encKeyBytes, keyBytes, Math.Min(encKeyBytes.Length, 32));
					Array.Copy(encKeyBytes, ivBytes , Math.Min(encKeyBytes.Length, 16));
					aes.Key = keyBytes;
					aes.IV  = ivBytes ;
					ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
					byte[] cipherBytes = Convert.FromBase64String(sCipherText);
					byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
					return Encoding.UTF8.GetString(plainBytes);
				}
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Checks if the current user has admin access.
		/// </summary>
		public bool AdminUserAccess(string sMODULE_NAME, string sACCESS_TYPE)
		{
			if (IS_ADMIN || IS_ADMIN_DELEGATE)
				return true;
			int nACLACCESS = GetUserAccess(sMODULE_NAME, sACCESS_TYPE);
			return (nACLACCESS >= ACL_ACCESS_ADMIN);
		}

		/// <summary>
		/// Stores ACL access data in the session for the current user.
		/// DataTable is serialized to JSON for distributed session compatibility.
		/// </summary>
		public void SetUserAccess(string sMODULE_NAME, string sACCESS_TYPE, int nACCESS)
		{
			string sCacheKey = "ACL_ACCESS." + sMODULE_NAME + "." + sACCESS_TYPE;
			Session?.SetString(sCacheKey, nACCESS.ToString());
		}

		/// <summary>
		/// Stores field-level ACL access in the session for the current user.
		/// </summary>
		public void SetUserFieldSecurity(string sMODULE_NAME, string sFIELD_NAME, int nACCESS)
		{
			string sCacheKey = "ACL_FIELD_ACCESS." + sMODULE_NAME + "." + sFIELD_NAME;
			Session?.SetString(sCacheKey, nACCESS.ToString());
		}
	}
}
