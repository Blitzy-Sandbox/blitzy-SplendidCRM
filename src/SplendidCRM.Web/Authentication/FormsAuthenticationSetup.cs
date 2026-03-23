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
 *********************************************************************************************************************/

// Required for AuthenticationBuilder — not in the implicit usings set for SDK Web projects
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SplendidCRM.Web.Authentication
{
    /// <summary>
    /// Configures ASP.NET Core Cookie authentication for Forms-based authentication mode.
    /// Replaces legacy ASP.NET <c>&lt;authentication mode="Forms"/&gt;</c> from Web.config with
    /// ASP.NET Core cookie authentication, preserving the SplendidCRM custom login endpoint,
    /// session-based identity model, and cookie security hardening from Global.asax.cs.
    /// 
    /// <para>
    /// Authentication flow overview:
    /// 1. User submits credentials to the custom login endpoint (/login or /Rest.svc/Login).
    /// 2. RestController (or login endpoint) calls Security.HashPassword for MD5 comparison
    ///    and SplendidInit.LoginUser to populate session with USER_ID, TEAM_ID, IS_ADMIN, etc.
    ///    // TECHNICAL DEBT: MD5 hash preserved for SugarCRM backward compatibility. Do not modify.
    /// 3. On successful validation, controller calls HttpContext.SignInAsync with a ClaimsPrincipal
    ///    containing USER_NAME and USER_ID claims.
    /// 4. Cookie middleware issues the authentication cookie (.SplendidCRM.Auth).
    /// 5. On subsequent requests, cookie middleware validates the cookie and restores the principal.
    /// 6. OnValidatePrincipal event verifies the distributed session is still active (mirrors
    ///    Security.IsAuthenticated() which checks !Sql.IsEmptyGuid(Security.USER_ID)).
    /// 7. For API requests (/Rest.svc/*, Accept: application/json), OnRedirectToLogin returns 401
    ///    instead of redirecting to the login page.
    /// </para>
    /// 
    /// <para>
    /// Source references:
    /// - Security.cs lines 355-358: IsAuthenticated() checks USER_ID in session
    /// - Security.cs lines 40-58: USER_ID property backed by HttpContext.Current.Session["USER_ID"]
    /// - Web.config line 100: sessionState timeout="20" — preserved as cookie ExpireTimeSpan
    /// - Global.asax.cs lines 149-190: Session_Start cookie hardening (SameSite, Secure flags)
    /// - Global.asax.cs lines 312-328: Application_AcquireRequestState sets GenericPrincipal
    /// </para>
    /// </summary>
    public static class FormsAuthenticationSetup
    {
        /// <summary>
        /// Default cookie expiration timeout in minutes, matching the legacy Web.config
        /// <c>&lt;sessionState mode="InProc" timeout="20"/&gt;</c> setting.
        /// Cookie expiration and distributed session timeout should match so that when the
        /// session expires server-side (Redis/SQL), the cookie is also invalidated.
        /// </summary>
        private const int DefaultCookieTimeoutMinutes = 20;

        /// <summary>
        /// Configuration key for overriding the default cookie timeout via the 5-tier
        /// configuration provider hierarchy (Secrets Manager → env vars → Parameter Store →
        /// appsettings.{env}.json → appsettings.json).
        /// </summary>
        private const string CookieTimeoutConfigKey = "Authentication:Forms:CookieTimeoutMinutes";

        /// <summary>
        /// The explicit cookie name for the Forms authentication cookie.
        /// </summary>
        private const string AuthCookieName = ".SplendidCRM.Auth";

        /// <summary>
        /// Adds ASP.NET Core Cookie authentication to the authentication builder, configured
        /// for SplendidCRM Forms-based authentication mode (AUTH_MODE=Forms).
        /// 
        /// <para>
        /// This method replaces the legacy ASP.NET <c>&lt;authentication mode="Forms"/&gt;</c>
        /// with ASP.NET Core cookie authentication. It preserves:
        /// - 20-minute session timeout from Web.config sessionState configuration
        /// - SameSite=Lax cookie policy from Global.asax.cs Session_Start hardening
        /// - Secure flag on HTTPS connections from Global.asax.cs Session_Start
        /// - Session-based authentication model (Security.USER_ID / Security.IsAuthenticated())
        /// - API 401 behavior for REST endpoints instead of login page redirect
        /// </para>
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/> to configure.</param>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> instance providing access to the 5-tier configuration
        /// provider hierarchy for reading optional cookie configuration overrides.
        /// </param>
        /// <returns>The <see cref="AuthenticationBuilder"/> for chaining.</returns>
        public static AuthenticationBuilder AddFormsAuthentication(
            this AuthenticationBuilder builder,
            IConfiguration configuration)
        {
            // Read configurable cookie timeout, defaulting to 20 minutes to match the legacy
            // Web.config <sessionState mode="InProc" timeout="20"/> setting.
            int cookieTimeoutMinutes = DefaultCookieTimeoutMinutes;
            string? configuredTimeout = configuration[CookieTimeoutConfigKey];
            if (!string.IsNullOrEmpty(configuredTimeout)
                && int.TryParse(configuredTimeout, out int parsedTimeout)
                && parsedTimeout > 0)
            {
                cookieTimeoutMinutes = parsedTimeout;
            }

            builder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                // --- Path Configuration ---
                // Custom login page endpoint; the actual login validation (MD5 hash comparison,
                // DB lookup) is handled by RestController or a dedicated login endpoint that
                // calls Security.HashPassword and SplendidInit.LoginUser.
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/login?error=access_denied";

                // --- Expiration Configuration ---
                // Match Web.config <sessionState mode="InProc" timeout="20"/> setting.
                // Cookie expiration and distributed session timeout should match (20 minutes)
                // so that when the session expires server-side (Redis/SQL), the cookie is
                // also invalidated via OnValidatePrincipal.
                options.ExpireTimeSpan = TimeSpan.FromMinutes(cookieTimeoutMinutes);

                // Extend cookie lifetime on each active request, matching the sliding
                // session behavior of the legacy InProc session state provider.
                options.SlidingExpiration = true;

                // --- Cookie Properties ---
                // Explicit cookie name for identification and consistent behavior across
                // deployment environments.
                options.Cookie.Name = AuthCookieName;

                // Security: prevent JavaScript access to the cookie (XSS mitigation).
                options.Cookie.HttpOnly = true;

                // Required for GDPR compliance — essential cookies are not subject to
                // consent requirements and must always be sent.
                options.Cookie.IsEssential = true;

                // Match Global.asax.cs Session_Start cookie hardening logic (lines 149-190).
                // SameSite=Lax prevents CSRF on top-level navigations while allowing the
                // cookie to be sent with same-site requests. This matches the legacy behavior
                // where SameSiteMode.Lax was set as the fallback for non-secure connections.
                options.Cookie.SameSite = SameSiteMode.Lax;

                // Secure flag on HTTPS connections — matches Global.asax.cs Session_Start
                // logic: if (Request.IsSecureConnection) Response.Cookies[...].Secure = true;
                // SameAsRequest ensures the Secure flag is set only when the request itself
                // is over HTTPS, supporting both HTTP development and HTTPS production.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // --- Event Handlers for SplendidCRM Integration ---
                options.Events = new CookieAuthenticationEvents
                {
                    // Validate that the SplendidCRM session is still active on each request.
                    // This mirrors Security.IsAuthenticated() which checks
                    // !Sql.IsEmptyGuid(Security.USER_ID). When the distributed session
                    // expires server-side (Redis/SQL timeout), the USER_ID will be absent,
                    // and the cookie principal should be rejected to force re-login.
                    OnValidatePrincipal = context =>
                    {
                        // Check that USER_ID exists in the distributed session.
                        // If the session has expired or been cleared, the cookie is stale
                        // and the user must re-authenticate.
                        string? userId = context.HttpContext.Session.GetString("USER_ID");
                        if (string.IsNullOrEmpty(userId))
                        {
                            // Session has expired or USER_ID was cleared (e.g., logout).
                            // Reject the principal to force cookie re-issuance via login.
                            context.RejectPrincipal();
                        }
                        return Task.CompletedTask;
                    },

                    // For API requests (Accept: application/json or /Rest.svc/ paths),
                    // return HTTP 401 Unauthorized instead of redirecting to the login page.
                    // This preserves the contract expected by the React SPA and any external
                    // API consumers that rely on 401 responses to trigger client-side login.
                    OnRedirectToLogin = context =>
                    {
                        if (IsApiRequest(context.HttpContext))
                        {
                            // API clients expect 401 Unauthorized, not a 302 redirect.
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        // Browser navigations get the standard redirect to the login page.
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
            });

            return builder;
        }

        /// <summary>
        /// Determines whether the current HTTP request is an API request that should receive
        /// a 401 Unauthorized status code instead of a redirect to the login page.
        /// 
        /// <para>
        /// An API request is identified by any of the following criteria:
        /// - The request path starts with <c>/Rest.svc</c> (main REST API — from Rest.svc.cs WCF conversion)
        /// - The request path starts with <c>/Administration/Rest.svc</c> (admin REST API)
        /// - The request path starts with <c>/api</c> (new ASP.NET Core API routes including /api/health)
        /// - The Accept header contains <c>application/json</c>
        /// </para>
        /// </summary>
        /// <param name="httpContext">The current <see cref="HttpContext"/>.</param>
        /// <returns><c>true</c> if the request is an API request; otherwise, <c>false</c>.</returns>
        private static bool IsApiRequest(HttpContext httpContext)
        {
            var request = httpContext.Request;

            // Check well-known API path prefixes — these are the WCF-to-Web-API converted
            // REST endpoints that must return 401 per AAP 0.7.3 Route Preservation Strategy.
            if (request.Path.StartsWithSegments("/Rest.svc", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (request.Path.StartsWithSegments("/Administration/Rest.svc", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check the Accept header for JSON content type — React SPA and other API clients
            // send Accept: application/json to indicate they expect JSON responses, not HTML.
            if (request.Headers.Accept.Any(h =>
                    h != null && h.Contains("application/json", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }
    }
}
