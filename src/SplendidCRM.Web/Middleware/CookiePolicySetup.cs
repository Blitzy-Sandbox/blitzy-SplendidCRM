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

// Migrated from Global.asax.cs Session_Start (lines 149-190) and DisallowsSameSiteNone (lines 110-147)
// Converts per-request Session_Start cookie hardening and user-agent-based SameSite sniffing
// to ASP.NET Core CookiePolicyOptions middleware configuration.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SplendidCRM.Web.Middleware
{
	/// <summary>
	/// Configures ASP.NET Core cookie policy options to replicate the SameSite/Secure cookie
	/// hardening behavior originally implemented in Global.asax.cs Session_Start.
	/// 
	/// The original implementation manually iterated session cookies on each request,
	/// setting Secure=true for HTTPS connections and applying SameSite policy based on
	/// user-agent sniffing for incompatible browsers. This static setup class converts
	/// that logic to ASP.NET Core's CookiePolicyOptions middleware pattern.
	/// 
	/// References:
	/// https://techcommunity.microsoft.com/t5/iis-support-blog/samesite-in-code-for-your-asp-net-applications/ba-p/1156361
	/// https://www.chromium.org/updates/same-site/incompatible-clients
	/// </summary>
	public static class CookiePolicySetup
	{
		/// <summary>
		/// Registers cookie policy options in the DI container that replicate the original
		/// Global.asax.cs Session_Start cookie hardening behavior.
		/// 
		/// Configures:
		/// - MinimumSameSitePolicy = SameSiteMode.Lax (AAP Section 0.7.6)
		/// - Secure = CookieSecurePolicy.SameAsRequest (matches original: Secure=true if HTTPS)
		/// - OnAppendCookie / OnDeleteCookie handlers for user-agent-based SameSite overrides
		/// 
		/// Usage in Program.cs:
		///   builder.Services.AddSplendidCookiePolicy();
		///   // ... later in the pipeline:
		///   app.UseCookiePolicy();
		/// </summary>
		/// <param name="services">The service collection to configure.</param>
		/// <returns>The service collection for chaining.</returns>
		public static IServiceCollection AddSplendidCookiePolicy(this IServiceCollection services)
		{
			services.Configure<CookiePolicyOptions>(options =>
			{
				// 08/05/2020 Paul.  Add support for SameSite using ASP.Net 4.7.2.
				// Migrated: MinimumSameSitePolicy replaces the per-cookie SameSite assignment in Session_Start.
				// AAP Section 0.7.6 specifies SameSiteMode.Lax as the default policy.
				options.MinimumSameSitePolicy = SameSiteMode.Lax;

				// Migrated: Original code set Secure=true only when Request.IsSecureConnection.
				// CookieSecurePolicy.SameAsRequest provides identical behavior — cookies are marked
				// Secure when the request is over HTTPS, matching the original conditional logic.
				options.Secure = CookieSecurePolicy.SameAsRequest;

				// 08/05/2020 Paul.  User-agent sniffing for browsers incompatible with SameSite=None.
				// The OnAppendCookie handler fires for every cookie being appended, allowing us to
				// override the SameSite attribute based on the requesting user-agent — replicating
				// the per-cookie manipulation that was in Session_Start.
				options.OnAppendCookie = appendContext =>
				{
					CheckSameSite(appendContext.Context, appendContext.CookieOptions);
				};

				// Apply the same user-agent sniffing logic when cookies are deleted,
				// ensuring consistent SameSite behavior across all cookie operations.
				options.OnDeleteCookie = deleteContext =>
				{
					CheckSameSite(deleteContext.Context, deleteContext.CookieOptions);
				};
			});

			return services;
		}

		/// <summary>
		/// Applies user-agent-based SameSite override to cookie options.
		/// 
		/// Replicates the original Session_Start logic:
		/// - If the user-agent is incompatible with SameSite=None (DisallowsSameSiteNone returns true),
		///   set SameSite to Unspecified so the attribute is not sent.
		/// - Otherwise, if the cookie is set to SameSite=None, allow it (the Secure flag is handled
		///   by CookieSecurePolicy.SameAsRequest at the policy level).
		/// 
		/// Original .NET Framework code:
		///   if (dissallowSameSiteFlag)
		///       Response.Cookies[sessionCookieName].SameSite = (SameSiteMode)(-1);
		///   else if (Request.IsSecureConnection)
		///       Response.Cookies[sessionCookieName].SameSite = SameSiteMode.None;
		///   else
		///       Response.Cookies[sessionCookieName].SameSite = SameSiteMode.Lax;
		/// 
		/// In ASP.NET Core, (SameSiteMode)(-1) is replaced by SameSiteMode.Unspecified.
		/// The else-if/else branches are handled by MinimumSameSitePolicy=Lax and
		/// the framework's built-in SameSite behavior for HTTPS connections.
		/// </summary>
		private static void CheckSameSite(HttpContext httpContext, CookieOptions options)
		{
			// Only intervene when the cookie is marked SameSite=None.
			// For SameSite=Lax or Strict, no user-agent override is needed.
			if (options.SameSite == SameSiteMode.None)
			{
				string userAgent = httpContext.Request.Headers["User-Agent"].ToString();
				// 08/05/2020 Paul.  ASP.Net 4.7.2 or higher is required to support SameSite property.
				// For browsers that do not properly handle SameSite=None, suppress the attribute entirely.
				if (DisallowsSameSiteNone(userAgent))
				{
					// In .NET Framework this was (SameSiteMode)(-1).
					// In ASP.NET Core, SameSiteMode.Unspecified = -1, which suppresses the SameSite
					// attribute from being sent, avoiding breakage in incompatible browsers.
					options.SameSite = SameSiteMode.Unspecified;
				}
			}
		}

		/// <summary>
		/// Determines whether the given user-agent string belongs to a browser that does not
		/// properly support the SameSite=None cookie attribute.
		/// 
		/// Migrated character-for-character from Global.asax.cs DisallowsSameSiteNone (lines 110-147).
		/// 
		/// Reference:
		/// https://techcommunity.microsoft.com/t5/iis-support-blog/samesite-in-code-for-your-asp-net-applications/ba-p/1156361
		/// https://www.chromium.org/updates/same-site/incompatible-clients
		/// </summary>
		/// <param name="userAgent">The User-Agent header value from the HTTP request.</param>
		/// <returns>True if the browser should NOT receive SameSite=None; false otherwise.</returns>
		private static bool DisallowsSameSiteNone(string userAgent)
		{
			// check if the user agent is null or empty
			if (String.IsNullOrWhiteSpace(userAgent))
				return false;

			// Cover all iOS based browsers here. This includes:
			// - Safari on iOS 12 for iPhone, iPod Touch, iPad
			// - WkWebview on iOS 12 for iPhone, iPod Touch, iPad
			// - Chrome on iOS 12 for iPhone, iPod Touch, iPad
			// All of which are broken by SameSite=None, because they use the iOS networking stack.
			if (userAgent.Contains("CPU iPhone OS 12") || userAgent.Contains("iPad; CPU OS 12"))
			{
				return true;
			}

			// Cover Mac OS X based browsers that use the Mac OS networking stack. 
			// This includes:
			// - Safari on Mac OS X.
			// This does not include:
			// - Chrome on Mac OS X
			// Because they do not use the Mac OS networking stack.
			if (userAgent.Contains("Macintosh; Intel Mac OS X 10_14") && userAgent.Contains("Version/") && userAgent.Contains("Safari"))
			{
				return true;
			}

			// Cover Chrome 50-69, because some versions are broken by SameSite=None, 
			// and none in this range require it.
			// Note: this covers some pre-Chromium Edge versions, 
			// but pre-Chromium Edge does not require SameSite=None.
			// https://www.chromium.org/updates/same-site/incompatible-clients
			if (userAgent.Contains("Chrome/5") || userAgent.Contains("Chrome/6") || userAgent.Contains("Android 6"))
			{
				return true;
			}
			return false;
		}
	}
}
