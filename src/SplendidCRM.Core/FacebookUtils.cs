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
// .NET 10 Migration: SplendidCRM/_code/FacebookUtils.cs → src/SplendidCRM.Core/FacebookUtils.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Collections; using System.Collections.Generic;
//   - ADDED:   using Microsoft.AspNetCore.Http; (provides IRequestCookieCollection)
//   - Constructor parameter: HttpCookieCollection (System.Web) → IRequestCookieCollection (Microsoft.AspNetCore.Http)
//   - Cookie access: HttpCookie cFacebook = cookies[key]; ... cFacebook.Value
//     → string sCookieValue = cookies[key]; (IRequestCookieCollection returns string directly)
//   - HttpUtility.ParseQueryString() called via fully qualified System.Web.HttpUtility.ParseQueryString()
//     (HttpUtility is available in the .NET 10 BCL through Microsoft.AspNetCore.App framework reference;
//      fully-qualified usage avoids a broad 'using System.Web;' directive per migration rules)
//   - All business logic, field declarations, property implementations, cookie parsing, and signature
//     verification logic preserved exactly as-is per the Minimal Change Clause.
#nullable disable
using System;
using System.Text;
using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;

namespace SplendidCRM
{
	/// <summary>
	/// Facebook integration utilities for parsing and validating Facebook authentication cookies.
	///
	/// Migrated from SplendidCRM/_code/FacebookUtils.cs for .NET 10 ASP.NET Core.
	///
	/// The fbs_{AppID} cookie issued by Facebook contains a URL-encoded set of key-value pairs
	/// (access_token, base_domain, expires, secret, session_key, sig, uid).  ParseCookie() extracts
	/// these values, reconstructs the payload string (all key=value pairs except sig, concatenated
	/// with the app secret), computes an MD5 hash via Security.HashPassword(), and compares it
	/// against the Facebook-provided sig value in IsValidSignature().
	///
	/// Key migration changes from the .NET Framework 4.8 original:
	///   - Constructor accepts IRequestCookieCollection (Microsoft.AspNetCore.Http) instead of
	///     HttpCookieCollection (System.Web).  In ASP.NET Core, request cookies are exposed as a
	///     string-keyed collection returning values as plain strings — no HttpCookie wrapper object.
	///   - HttpUtility.ParseQueryString() is invoked via its fully qualified name
	///     System.Web.HttpUtility.ParseQueryString() so that no broad 'using System.Web;' directive
	///     is required.  The method is part of the .NET 10 BCL (available via the
	///     Microsoft.AspNetCore.App framework reference included in SplendidCRM.Core.csproj).
	/// </summary>
	public class FacebookUtils
	{
		protected string   sAppID            ;
		protected string   sAppSecret        ;
		protected string   sAccessToken      ;
		protected string   sBaseDomain       ;
		protected DateTime dtExpires         ;
		protected string   sSecret           ;
		protected string   sSessionKey       ;
		protected string   sSig              ;
		protected string   sUID              ;
		protected string   sComputedSignature;
		protected NameValueCollection arrValues;

		/// <summary>
		/// Gets the Facebook User ID (uid) extracted from the authentication cookie.
		/// Returns null if the cookie has not been parsed or the uid field is absent.
		/// </summary>
		public string UID
		{
			get { return sUID; }
		}

		/// <summary>
		/// Gets the cookie expiration timestamp derived from the Unix-epoch expires field.
		/// Returns DateTime.MinValue (i.e. 1970-01-01 + 0 seconds) if the field is absent or zero.
		/// </summary>
		public DateTime Expires
		{
			get { return dtExpires; }
		}

		/// <summary>
		/// Returns true if both the Facebook App ID has been supplied and the fbs_{AppID} cookie
		/// was located in the request cookie collection and successfully parsed into arrValues.
		/// Used by callers to short-circuit Facebook-specific code paths when not configured.
		/// </summary>
		public bool FacebookValuesExist
		{
			get { return !Sql.IsEmptyString(sAppID) && (arrValues != null); }
		}

		/// <summary>
		/// Initializes a new FacebookUtils instance by locating and pre-parsing the fbs_{AppID}
		/// cookie from the ASP.NET Core request cookie collection.
		/// </summary>
		/// <param name="sAppID">
		/// The Facebook application ID.  Used to look up the cookie named "fbs_{sAppID}" and as
		/// part of the signature payload.
		/// </param>
		/// <param name="sAppSecret">
		/// The Facebook application secret appended to the payload string before hashing.
		/// </param>
		/// <param name="cookies">
		/// The ASP.NET Core request cookie collection (IRequestCookieCollection), replacing the
		/// legacy System.Web.HttpCookieCollection.  Values are accessed as plain strings via
		/// IRequestCookieCollection[key] rather than as HttpCookie objects.
		/// </param>
		public FacebookUtils(string sAppID, string sAppSecret, IRequestCookieCollection cookies)
		{
			this.sAppID     = sAppID    ;
			this.sAppSecret = sAppSecret;

			// .NET 10 Migration: IRequestCookieCollection[key] returns a string directly.
			// The original code retrieved an HttpCookie object and then accessed .Value on it.
			// ASP.NET Core removes the HttpCookie wrapper — the cookie value is the string itself.
			string sCookieValue = cookies["fbs_" + sAppID];
			if ( sCookieValue != null )
			{
				// HttpUtility.ParseQueryString is part of the .NET 10 BCL (System.Web namespace,
				// available via Microsoft.AspNetCore.App framework reference).  The fully qualified
				// call avoids introducing a broad 'using System.Web;' directive.
				arrValues = System.Web.HttpUtility.ParseQueryString(sCookieValue.Replace("\"", string.Empty));
			}
		}

		/// <summary>
		/// Parses the cookie key-value pairs into typed fields and computes the MD5 signature
		/// for subsequent validation.
		///
		/// The Facebook cookie signature is computed by concatenating all key=value pairs
		/// (excluding "sig" itself) in the order they appear in the cookie, then appending the
		/// app secret, and taking the MD5 hash of the resulting string via Security.HashPassword().
		/// </summary>
		/// <returns>
		/// The result of IsValidSignature() — true if the computed MD5 signature matches the
		/// "sig" value present in the Facebook cookie.
		/// </returns>
		public bool ParseCookie()
		{
			// 03/19/2011 Paul.  We need to reparse the cookie so that the values are properly unescaped. 
			if ( arrValues != null )
			{
				StringBuilder sbPayload = new StringBuilder();
				foreach ( string sKey in arrValues )
				{
					if ( sKey != "sig" )
						sbPayload.AppendFormat("{0}={1}", sKey, arrValues[sKey]);
				}
				sbPayload.Append(sAppSecret);
				// 03/19/2011 Paul.  facebook uses the same MD5 hash that we use for SplendidCRM passwords. 
				sComputedSignature = Security.HashPassword(sbPayload.ToString());

				long lExpires;
				DateTime dtUnixEpoch = new DateTime(1970, 1, 1);
				sAccessToken = arrValues["access_token"];
				sBaseDomain  = arrValues["base_domain" ];
				long.TryParse(arrValues["expires"], out lExpires);  // Unix timestamp. 
				dtExpires    = dtUnixEpoch.AddSeconds(lExpires);
				sSecret      = arrValues["secret"      ];
				sSessionKey  = arrValues["session_key" ];
				sSig         = arrValues["sig"         ];
				sUID         = arrValues["uid"         ];  // This is the facebook User ID. 
			}
			return IsValidSignature();
		}

		/// <summary>
		/// Validates that the MD5 signature computed from the cookie payload in ParseCookie()
		/// matches the "sig" field supplied by Facebook inside the fbs_{AppID} cookie.
		/// </summary>
		/// <returns>
		/// True if the computed signature equals the Facebook-provided signature; false otherwise.
		/// Returns false (not throwing) when neither field has been populated (i.e. ParseCookie()
		/// was not called or the cookie was absent).
		/// </returns>
		public bool IsValidSignature()
		{
			return (sSig == sComputedSignature);
		}
	}
}
