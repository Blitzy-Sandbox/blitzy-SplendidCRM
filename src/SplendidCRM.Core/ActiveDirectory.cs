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
// .NET 10 Migration: SplendidCRM/_code/ActiveDirectory.cs → src/SplendidCRM.Core/ActiveDirectory.cs
// Key changes applied per AAP Cross-Cutting rules (minimal change clause observed):
//   - REMOVED: using System.Web; (HttpApplicationState does not exist in ASP.NET Core / .NET 10)
//   - ADDED:   using Microsoft.AspNetCore.Http; (provides HttpContext, IHttpContextAccessor)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (provides IMemoryCache, replacing HttpApplicationState)
//   - CHANGED: HttpApplicationState Application parameter → IMemoryCache Application in all static method
//              signatures: AzureValidate, Office365RefreshAccessToken, Office365TestAccessToken, GetProfile
//   - CHANGED: HttpContext parameter type is now Microsoft.AspNetCore.Http.HttpContext (was System.Web.HttpContext)
//   - ADDED:   DI constructor (IHttpContextAccessor, IMemoryCache) for ASP.NET Core service registration
//   - PRESERVED: Namespace SplendidCRM, all public class/method signatures, all exception-throwing stub
//                behavior, Office365AccessToken data carrier, MicrosoftGraphProfile data carrier
#nullable disable
using System;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Office 365 OAuth2 access token data carrier.
	/// Serialized to/from JSON by the Office 365 OAuth2 token endpoint.
	/// Migrated from SplendidCRM/_code/ActiveDirectory.cs for .NET 10.
	/// </summary>
	//[DataContract]
	public class Office365AccessToken
	{
		//[DataMember]
		public string token_type    { get; set; }
		//[DataMember]
		public string scope         { get; set; }
		//[DataMember]
		public string expires_in    { get; set; }
		//[DataMember]
		public string expires_on    { get; set; }
		//[DataMember]
		public string access_token  { get; set; }
		//[DataMember]
		public string refresh_token { get; set; }

		/// <summary>Alias property returning access_token raw string value.</summary>
		public string AccessToken
		{
			get { return access_token;  }
			set { access_token = value; }
		}

		/// <summary>Alias property returning refresh_token raw string value.</summary>
		public string RefreshToken
		{
			get { return refresh_token;  }
			set { refresh_token = value; }
		}

		/// <summary>
		/// Typed Int64 accessor for the expires_in field.
		/// Uses Sql.ToInt64 / Sql.ToString for conversion to maintain consistent null-safe parsing.
		/// </summary>
		public Int64 ExpiresInSeconds
		{
			get { return Sql.ToInt64(expires_in);  }
			set { expires_in = Sql.ToString(value); }
		}

		/// <summary>Alias property returning token_type raw string value.</summary>
		public string TokenType
		{
			get { return token_type;  }
			set { token_type = value; }
		}
	}

	/// <summary>
	/// Microsoft Graph API user profile data carrier.
	/// Maps to the /v1.0/me JSON response from Microsoft Graph.
	/// See: https://graph.microsoft.io/en-us/docs
	/// Migrated from SplendidCRM/_code/ActiveDirectory.cs for .NET 10.
	/// </summary>
	// https://graph.microsoft.io/en-us/docs
	//[DataContract]
	public class MicrosoftGraphProfile
	{
		//[DataMember] 
		public string   id                { get; set; }
		//[DataMember] 
		public string   userPrincipalName { get; set; }
		//[DataMember] 
		public string   displayName       { get; set; }
		//[DataMember] 
		public string   givenName         { get; set; }
		//[DataMember] 
		public string   surname           { get; set; }
		//[DataMember] 
		public string   jobTitle          { get; set; }
		//[DataMember] 
		public string   mail              { get; set; }
		//[DataMember] 
		public string   officeLocation    { get; set; }
		//[DataMember] 
		public string   preferredLanguage { get; set; }
		//[DataMember] 
		public string   mobilePhone       { get; set; }
		//[DataMember] 
		public string[] businessPhones    { get; set; }

		/// <summary>Alias for displayName.</summary>
		public string Name
		{
			get { return displayName; }
			set { displayName = value; }
		}

		/// <summary>Alias for givenName.</summary>
		public string FirstName
		{
			get { return givenName; }
			set { givenName = value; }
		}

		/// <summary>Alias for surname.</summary>
		public string LastName
		{
			get { return surname; }
			set { surname = value; }
		}

		/// <summary>Alias for userPrincipalName.</summary>
		public string UserName
		{
			get { return userPrincipalName; }
			set { userPrincipalName = value; }
		}

		/// <summary>Alias for mail.</summary>
		public string EmailAddress
		{
			get { return mail; }
			set { mail = value; }
		}
	}

	/// <summary>
	/// Active Directory integration stub.
	/// Provides Azure AD / ADFS / Office 365 SSO integration extension points.
	/// All methods are Enterprise Edition stubs that throw NotSupportedException-equivalent exceptions.
	/// 
	/// .NET 10 Migration notes:
	///   - System.Web.HttpContext → Microsoft.AspNetCore.Http.HttpContext (method parameters)
	///   - System.Web.HttpApplicationState → Microsoft.Extensions.Caching.Memory.IMemoryCache (method parameters)
	///   - DI constructor added for ASP.NET Core service registration pattern
	///   - All stub behavior (exception throwing) preserved exactly per AAP minimal change clause
	/// </summary>
	public class ActiveDirectory
	{
		// =====================================================================================
		// .NET 10 Migration: DI constructor for ASP.NET Core service registration.
		// Replaces the implicit static-only usage pattern from the original .NET Framework file.
		// When registered as a scoped/singleton service, IHttpContextAccessor and IMemoryCache
		// are injected automatically by the DI container.
		// =====================================================================================
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache         _memoryCache;

		/// <summary>
		/// DI constructor for ASP.NET Core dependency injection.
		/// Register this class via services.AddScoped&lt;ActiveDirectory&gt;() in Program.cs.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// Replaces HttpContext.Current static access; injected by ASP.NET Core DI.
		/// </param>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) global state; injected by ASP.NET Core DI.
		/// </param>
		public ActiveDirectory(IHttpContextAccessor httpContextAccessor, IMemoryCache memoryCache)
		{
			_httpContextAccessor = httpContextAccessor;
			_memoryCache         = memoryCache;
		}

		/// <summary>
		/// Initiates an Azure AD single-sign-on login redirect.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for constructing the OAuth redirect URL and writing the redirect response.
		/// </param>
		/// <returns>The Azure AD authorization URL for redirect.</returns>
		public static string AzureLogin(HttpContext Context)
		{
			throw(new Exception("Azure Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Initiates an Azure AD single-sign-on logout redirect.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for constructing the Azure logout URL and clearing the local session.
		/// </param>
		/// <returns>The Azure AD logout URL for redirect.</returns>
		// 12/25/2018 Paul.  Logout should perform Azure or ADFS logout. 
		public static string AzureLogout(HttpContext Context)
		{
			throw(new Exception("Azure Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Validates an Azure AD OAuth2 authorization code or ID token and returns the matching user ID.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Application">
		/// IMemoryCache instance (replaces System.Web.HttpApplicationState Application parameter).
		/// Used for token caching and application-scoped configuration lookups.
		/// </param>
		/// <param name="sToken">The Azure AD authorization code or token string to validate.</param>
		/// <param name="sError">Output error message if validation fails.</param>
		/// <returns>The SplendidCRM user Guid mapped to the validated Azure identity.</returns>
		public static Guid AzureValidate(IMemoryCache Application, string sToken, ref string sError)
		{
			throw(new Exception("Azure Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Validates an Azure AD JSON Web Token (JWT) and returns the matching user ID.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for reading the Authorization header and writing authentication cookies.
		/// </param>
		/// <param name="sToken">The JWT bearer token string to validate.</param>
		/// <param name="bMobileClient">True if the request originates from a mobile client.</param>
		/// <param name="sError">Output error message if validation fails.</param>
		/// <returns>The SplendidCRM user Guid mapped to the validated JWT identity.</returns>
		public static Guid AzureValidateJwt(HttpContext Context, string sToken, bool bMobileClient, ref string sError)
		{
			throw(new Exception("Azure Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Initiates an AD Federation Services (ADFS) single-sign-on login redirect.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for constructing the ADFS redirect URL and writing the redirect response.
		/// </param>
		/// <returns>The ADFS authorization URL for redirect.</returns>
		public static string FederationServicesLogin(HttpContext Context)
		{
			throw(new Exception("ADFS Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Initiates an AD Federation Services (ADFS) single-sign-on logout redirect.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for constructing the ADFS logout URL and clearing the local session.
		/// </param>
		/// <returns>The ADFS logout URL for redirect.</returns>
		// 12/25/2018 Paul.  Logout should perform Azure or ADFS logout. 
		public static string FederationServicesLogout(HttpContext Context)
		{
			throw(new Exception("ADFS Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Validates an ADFS token and returns the matching user ID.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for reading SAML assertions from the request.
		/// </param>
		/// <param name="sToken">The ADFS SAML token string to validate.</param>
		/// <param name="sError">Output error message if validation fails.</param>
		/// <returns>The SplendidCRM user Guid mapped to the validated ADFS identity.</returns>
		public static Guid FederationServicesValidate(HttpContext Context, string sToken, ref string sError)
		{
			throw(new Exception("ADFS Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Validates ADFS credentials (username/password) and returns the matching user ID.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for reading request context during credential validation.
		/// </param>
		/// <param name="sUSER_NAME">The username presented for ADFS authentication.</param>
		/// <param name="sPASSWORD">The password presented for ADFS authentication.</param>
		/// <param name="sError">Output error message if validation fails.</param>
		/// <returns>The SplendidCRM user Guid mapped to the validated ADFS identity.</returns>
		public static Guid FederationServicesValidate(HttpContext Context, string sUSER_NAME, string sPASSWORD, ref string sError)
		{
			throw(new Exception("ADFS Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Validates an ADFS JSON Web Token (JWT) and returns the matching user ID.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for reading the Authorization header and writing authentication cookies.
		/// </param>
		/// <param name="sToken">The JWT bearer token string to validate.</param>
		/// <param name="bMobileClient">True if the request originates from a mobile client.</param>
		/// <param name="sError">Output error message if validation fails.</param>
		/// <returns>The SplendidCRM user Guid mapped to the validated JWT identity.</returns>
		public static Guid FederationServicesValidateJwt(HttpContext Context, string sToken, bool bMobileClient, ref string sError)
		{
			throw(new Exception("ADFS Single-Sign-On is not supported."));
		}

		/// <summary>
		/// Acquires an Office 365 OAuth2 access token using an authorization code.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Context">
		/// ASP.NET Core HttpContext (replaces System.Web.HttpContext).
		/// Used for reading request parameters and writing the token response.
		/// </param>
		/// <param name="sOAuthDirectoryTenatID">
		/// Azure Active Directory tenant ID for single-tenant app registrations.
		/// Required since 02/04/2023 per AAP comment.
		/// </param>
		/// <param name="sOAuthClientID">The Azure AD application (client) ID.</param>
		/// <param name="sOAuthClientSecret">The Azure AD application client secret.</param>
		/// <param name="gUSER_ID">The SplendidCRM user ID for token association.</param>
		/// <param name="sAuthorizationCode">The OAuth2 authorization code received from Azure AD.</param>
		/// <param name="sRedirect">
		/// The redirect URI registered with Azure AD.
		/// Added 11/09/2019 to support calls from the React client.
		/// </param>
		/// <returns>The Office 365 access token data carrier with bearer and refresh tokens.</returns>
		// 11/09/2019 Paul.  Pass the RedirectURL so that we can call from the React client. 
		// 02/04/2023 Paul.  Directory Tenant is now required for single tenant app registrations. 
		public static Office365AccessToken Office365AcquireAccessToken(HttpContext Context, string sOAuthDirectoryTenatID, string sOAuthClientID, string sOAuthClientSecret, Guid gUSER_ID, string sAuthorizationCode, string sRedirect)
		{
			throw(new Exception("Office 365 integration is not supported."));
		}

		/// <summary>
		/// Refreshes an existing Office 365 OAuth2 access token using the stored refresh token.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Application">
		/// IMemoryCache instance (replaces System.Web.HttpApplicationState Application parameter).
		/// Used for reading and writing the cached access and refresh tokens.
		/// </param>
		/// <param name="sOAuthDirectoryTenatID">
		/// Azure Active Directory tenant ID for single-tenant app registrations.
		/// Required since 02/04/2023 per AAP comment.
		/// </param>
		/// <param name="sOAuthClientID">The Azure AD application (client) ID.</param>
		/// <param name="sOAuthClientSecret">The Azure AD application client secret.</param>
		/// <param name="gUSER_ID">The SplendidCRM user ID whose token is being refreshed.</param>
		/// <param name="bForceRefresh">If true, refresh even if the current token has not expired.</param>
		/// <returns>The refreshed Office 365 access token data carrier.</returns>
		// 02/04/2023 Paul.  Directory Tenant is now required for single tenant app registrations. 
		public static Office365AccessToken Office365RefreshAccessToken(IMemoryCache Application, string sOAuthDirectoryTenatID, string sOAuthClientID, string sOAuthClientSecret, Guid gUSER_ID, bool bForceRefresh)
		{
			throw(new Exception("Office 365 integration is not supported."));
		}

		/// <summary>
		/// Tests whether a stored Office 365 OAuth2 access token is still valid.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Application">
		/// IMemoryCache instance (replaces System.Web.HttpApplicationState Application parameter).
		/// Used for reading the cached access token to test against the Graph API.
		/// </param>
		/// <param name="sOAuthDirectoryTenatID">
		/// Azure Active Directory tenant ID for single-tenant app registrations.
		/// Required since 02/04/2023 per AAP comment.
		/// </param>
		/// <param name="sOAuthClientID">The Azure AD application (client) ID.</param>
		/// <param name="sOAuthClientSecret">The Azure AD application client secret.</param>
		/// <param name="gUSER_ID">The SplendidCRM user ID whose token is being tested.</param>
		/// <param name="sbErrors">
		/// StringBuilder for accumulating error messages encountered during the token test.
		/// </param>
		/// <returns>True if the access token is valid; false otherwise.</returns>
		// 02/04/2023 Paul.  Directory Tenant is now required for single tenant app registrations. 
		public static bool Office365TestAccessToken(IMemoryCache Application, string sOAuthDirectoryTenatID, string sOAuthClientID, string sOAuthClientSecret, Guid gUSER_ID, StringBuilder sbErrors)
		{
			throw(new Exception("Office 365 integration is not supported."));
		}

		/// <summary>
		/// Retrieves the Microsoft Graph user profile for the authenticated user.
		/// Enterprise Edition stub — not supported in Community Edition.
		/// </summary>
		/// <param name="Application">
		/// IMemoryCache instance (replaces System.Web.HttpApplicationState Application parameter).
		/// Used for reading the cached access token required for Graph API calls.
		/// </param>
		/// <param name="sToken">The bearer access token for the Microsoft Graph API request.</param>
		/// <returns>The Microsoft Graph user profile data carrier.</returns>
		public static MicrosoftGraphProfile GetProfile(IMemoryCache Application, string sToken)
		{
			throw(new Exception("Office 365 integration is not supported."));
		}
	}
}
