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
#nullable disable
using System;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Google Apps integration utilities.
	/// Migrated from .NET Framework 4.8 to .NET 10: replaced HttpApplicationState with IMemoryCache via DI,
	/// replaced System.Web.HttpContext with Microsoft.AspNetCore.Http.HttpContext.
	/// This is a dormant Enterprise Edition stub — methods throw NotSupportedException in Community Edition.
	/// </summary>
	public class GoogleApps
	{
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// Initializes a new instance of <see cref="GoogleApps"/> with dependency-injected services.
		/// Replaces the former static class pattern where HttpApplicationState was passed as a method parameter.
		/// </summary>
		/// <param name="memoryCache">Memory cache instance replacing HttpApplicationState for application-level state.</param>
		public GoogleApps(IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
		}

		/// <summary>
		/// Checks whether GoogleApps integration is enabled.
		/// Community Edition stub — always returns false.
		/// </summary>
		/// <returns>False in Community Edition; true when Enterprise GoogleApps integration is configured.</returns>
		public bool GoogleAppsEnabled()
		{
			return false;
		}

		/// <summary>
		/// Refreshes the OAuth2 access token for a given user.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="gUSER_ID">The user ID for whom to refresh the access token.</param>
		/// <param name="bForceRefresh">Whether to force a token refresh regardless of expiration.</param>
		/// <returns>A TokenResponse containing the refreshed access token.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public Google.Apis.Auth.OAuth2.Responses.TokenResponse RefreshAccessToken(Guid gUSER_ID, bool bForceRefresh)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Tests the OAuth2 access token for a given user.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="gUSER_ID">The user ID to test.</param>
		/// <param name="sbErrors">StringBuilder to collect any error messages.</param>
		/// <returns>True if access token is valid; false otherwise.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public bool TestAccessToken(Guid gUSER_ID, StringBuilder sbErrors)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Gets the email address associated with a user's Google account.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="gUSER_ID">The user ID whose email address to retrieve.</param>
		/// <param name="sbErrors">StringBuilder to collect any error messages.</param>
		/// <returns>The user's Google email address.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public string GetEmailAddress(Guid gUSER_ID, StringBuilder sbErrors)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Tests access to a specific mailbox for a given user.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="gUSER_ID">The user ID whose mailbox to test.</param>
		/// <param name="sMAILBOX">The mailbox identifier to test.</param>
		/// <param name="sbErrors">StringBuilder to collect any error messages.</param>
		/// <returns>True if mailbox is accessible; false otherwise.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public bool TestMailbox(Guid gUSER_ID, string sMAILBOX, StringBuilder sbErrors)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Sends a test email message via Google Apps.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="gOAUTH_TOKEN_ID">The OAuth token ID to use for sending.</param>
		/// <param name="sFromAddress">The sender email address.</param>
		/// <param name="sFromName">The sender display name.</param>
		/// <param name="sToAddress">The recipient email address.</param>
		/// <param name="sToName">The recipient display name.</param>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public void SendTestMessage(Guid gOAUTH_TOKEN_ID, string sFromAddress, string sFromName, string sToAddress, string sToName)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Retrieves a MIME message from Google Apps for a specific user and unique ID.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="Context">The current HTTP context (ASP.NET Core HttpContext).</param>
		/// <param name="gUSER_ID">The user ID for the message lookup.</param>
		/// <param name="sUNIQUE_ID">The unique identifier of the message to retrieve.</param>
		/// <returns>A MimeKit MimeMessage instance.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public MimeKit.MimeMessage GetMimeMessage(HttpContext Context, Guid gUSER_ID, string sUNIQUE_ID)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Marks a message as read in Google Apps.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="Context">The current HTTP context (ASP.NET Core HttpContext).</param>
		/// <param name="gUSER_ID">The user ID for the message operation.</param>
		/// <param name="sUNIQUE_ID">The unique identifier of the message to mark as read.</param>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public void MarkAsRead(HttpContext Context, Guid gUSER_ID, string sUNIQUE_ID)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Marks a message as unread in Google Apps.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="Context">The current HTTP context (ASP.NET Core HttpContext).</param>
		/// <param name="gUSER_ID">The user ID for the message operation.</param>
		/// <param name="sUNIQUE_ID">The unique identifier of the message to mark as unread.</param>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public void MarkAsUnread(HttpContext Context, Guid gUSER_ID, string sUNIQUE_ID)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Retrieves messages from a specific Google Apps folder.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="Context">The current HTTP context (ASP.NET Core HttpContext).</param>
		/// <param name="gUSER_ID">The user ID for the folder lookup.</param>
		/// <param name="sFOLDER_ID">The folder identifier to retrieve messages from.</param>
		/// <param name="bONLY_SINCE">Whether to retrieve only messages since the last email UID.</param>
		/// <param name="nLAST_EMAIL_UID">The UID of the last retrieved email for incremental retrieval.</param>
		/// <param name="nMaxRecords">Maximum number of records to return.</param>
		/// <returns>A DataTable containing the folder messages.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public DataTable GetFolderMessages(HttpContext Context, Guid gUSER_ID, string sFOLDER_ID, bool bONLY_SINCE, long nLAST_EMAIL_UID, int nMaxRecords)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}
	}
}

namespace Google.Apis.Auth.OAuth2
{
	/// <summary>
	/// Stub replacement for Google.Apis.Auth.OAuth2.ClientSecrets.
	/// Provides OAuth2 client credentials for Google API authentication flows.
	/// This is a local shim class replacing the Google.Apis NuGet package dependency.
	/// </summary>
	public class ClientSecrets
	{
		public string ClientId                 { get; set; }
		public string ClientSecret             { get; set; }
	}
}

namespace Google.Apis.Auth.OAuth2.Responses
{
	/// <summary>
	/// Stub replacement for Google.Apis.Auth.OAuth2.Responses.TokenResponse.
	/// Represents an OAuth2 token response containing access token, refresh token, and expiration data.
	/// This is a local shim class replacing the Google.Apis NuGet package dependency.
	/// </summary>
	public class TokenResponse
	{
		public string AccessToken              { get; set; }
		public string TokenType                { get; set; }
		public string RefreshToken             { get; set; }
		public long?  ExpiresInSeconds         { get; set; }
	}
}

namespace Google.Apis.Contacts.v3.Data
{
	/// <summary>
	/// Stub replacement for Google.Apis.Contacts.v3.Data.StructuredPostalAddress.
	/// Represents a structured postal address from Google Contacts API.
	/// This is a local shim class replacing the Google.Apis NuGet package dependency.
	/// </summary>
	public class StructuredPostalAddress
	{
		public string         Rel              { get; set; }
		public Nullable<bool> Primary          { get; set; }
		public string         Agent            { get; set; }
		public string         HouseName        { get; set; }
		public string         Street           { get; set; }
		public string         POBox            { get; set; }
		public string         Neighborhood     { get; set; }
		public string         City             { get; set; }
		public string         County           { get; set; }
		public string         State            { get; set; }
		public string         PostalCode       { get; set; }
		public string         Country          { get; set; }
		public string         FormattedAddress { get; set; }
	}
}

namespace Google.Apis.Auth.OAuth2.Flows
{
	/// <summary>
	/// Stub replacement for Google.Apis.Auth.OAuth2.Flows.AuthorizationCodeFlow.
	/// Base class providing OAuth2 authorization code flow initialization configuration.
	/// This is a local shim class replacing the Google.Apis NuGet package dependency.
	/// </summary>
	public class AuthorizationCodeFlow
	{
		/// <summary>
		/// Initialization configuration for an authorization code flow,
		/// including client secrets and requested OAuth2 scopes.
		/// </summary>
		public class Initializer
		{
			public ClientSecrets ClientSecrets { get; set; }
			public IEnumerable<string> Scopes  { get; set; }
		}
	}

	/// <summary>
	/// Stub replacement for Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.
	/// Provides Google-specific OAuth2 authorization code flow with token exchange.
	/// Community Edition stub — ExchangeCodeForTokenAsync throws indicating integration is not supported.
	/// This is a local shim class replacing the Google.Apis NuGet package dependency.
	/// </summary>
	public class GoogleAuthorizationCodeFlow
	{
		/// <summary>
		/// Constructs a GoogleAuthorizationCodeFlow with the specified initializer configuration.
		/// </summary>
		/// <param name="initializer">The initialization configuration for this flow.</param>
		public GoogleAuthorizationCodeFlow(GoogleAuthorizationCodeFlow.Initializer initializer)
		{
		}

		/// <summary>
		/// Exchanges an authorization code for an OAuth2 access token.
		/// Community Edition stub — throws exception indicating GoogleApps integration is not supported.
		/// </summary>
		/// <param name="userId">The user identifier for the token exchange.</param>
		/// <param name="code">The authorization code received from the OAuth2 authorization endpoint.</param>
		/// <param name="redirectUri">The redirect URI used in the authorization request.</param>
		/// <param name="taskCancellationToken">Cancellation token for the async operation.</param>
		/// <returns>A Task containing the TokenResponse with access and refresh tokens.</returns>
		/// <exception cref="Exception">Always thrown in Community Edition.</exception>
		public Task<Responses.TokenResponse> ExchangeCodeForTokenAsync(string userId, string code, string redirectUri, CancellationToken taskCancellationToken)
		{
			throw(new Exception("GoogleApps integration is not supported."));
		}

		/// <summary>
		/// Google-specific initialization configuration extending the base AuthorizationCodeFlow.Initializer.
		/// </summary>
		public class Initializer : AuthorizationCodeFlow.Initializer
		{
			/// <summary>
			/// Constructs a new Google-specific Initializer with default settings.
			/// </summary>
			public Initializer()
			{
			}
		}
	}
}
