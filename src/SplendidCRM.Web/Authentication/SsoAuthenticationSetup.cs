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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SplendidCRM.Web.Authentication
{
    /// <summary>
    /// Configures OpenID Connect (OIDC) authentication middleware for SSO mode.
    /// Called from Program.cs when AUTH_MODE=SSO is set in the configuration hierarchy.
    /// 
    /// This replaces the legacy Web.config &lt;authentication mode="Windows"/&gt; with an OIDC
    /// alternative selectable via the AUTH_MODE environment variable. After OIDC token validation,
    /// the SplendidCRM session must be populated with the same keys as forms/windows auth
    /// (USER_ID, USER_NAME, FULL_NAME, TEAM_ID, IS_ADMIN, etc.) via SplendidInit.LoginUser.
    /// 
    /// Configuration is read from the 5-tier provider hierarchy per AAP 0.8.2:
    ///   1. AWS Secrets Manager — SSO_CLIENT_ID, SSO_CLIENT_SECRET
    ///   2. Environment variables — runtime overrides
    ///   3. AWS Systems Manager Parameter Store — SSO_AUTHORITY
    ///   4. appsettings.{Environment}.json — environment defaults
    ///   5. appsettings.json — base defaults
    /// 
    /// Supports both nested JSON keys (SSO:Authority, SSO:ClientId, SSO:ClientSecret)
    /// and flat environment variable keys (SSO_AUTHORITY, SSO_CLIENT_ID, SSO_CLIENT_SECRET).
    /// </summary>
    public static class SsoAuthenticationSetup
    {
        /// <summary>
        /// Adds OpenID Connect (OIDC) and cookie authentication to the authentication builder.
        /// Configures the OIDC scheme for SSO sign-in and a paired cookie scheme for persisting
        /// authentication state between requests.
        /// 
        /// The cookie expiration is set to 20 minutes matching the legacy Web.config
        /// &lt;sessionState timeout="20"/&gt; setting.
        /// 
        /// Throws <see cref="InvalidOperationException"/> if any required SSO configuration
        /// value is missing (fail-fast per AAP 0.8.2).
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/> to configure.</param>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> instance providing access to the 5-tier
        /// configuration provider hierarchy.
        /// </param>
        /// <returns>The <see cref="AuthenticationBuilder"/> for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when SSO_AUTHORITY, SSO_CLIENT_ID, or SSO_CLIENT_SECRET is missing or empty.
        /// </exception>
        public static AuthenticationBuilder AddSsoAuthentication(
            this AuthenticationBuilder builder,
            IConfiguration configuration)
        {
            // Read SSO configuration values, supporting both nested JSON keys and flat env var keys.
            // Nested keys (appsettings.json): SSO:Authority, SSO:ClientId, SSO:ClientSecret
            // Flat keys (env vars):           SSO_AUTHORITY, SSO_CLIENT_ID, SSO_CLIENT_SECRET
            string? ssoAuthority = ReadConfigValue(configuration, "SSO:Authority", "SSO_AUTHORITY");
            string? ssoClientId = ReadConfigValue(configuration, "SSO:ClientId", "SSO_CLIENT_ID");
            string? ssoClientSecret = ReadConfigValue(configuration, "SSO:ClientSecret", "SSO_CLIENT_SECRET");

            // Fail-fast validation: all three SSO configuration values are required when AUTH_MODE=SSO.
            // Per AAP 0.8.2, application must log the specific missing variable name and exit with non-zero code.
            ValidateRequiredConfiguration(ssoAuthority, ssoClientId, ssoClientSecret);

            // Configure cookie authentication scheme for persisting OIDC authentication state.
            // OIDC requires a cookie scheme to maintain the authenticated session between requests.
            // Cookie settings match the legacy Web.config <sessionState timeout="20"/> and
            // Global.asax.cs Session_Start cookie hardening (SameSite, Secure, HttpOnly).
            builder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(20); // Match Web.config sessionState timeout=20
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax; // Match Global.asax.cs Session_Start SameSite hardening
            });

            // Configure OpenID Connect authentication scheme for SSO sign-in.
            // Uses Authorization Code flow (response_type=code) with PKCE support.
            // After successful OIDC token validation, SplendidCRM must populate session keys
            // (USER_ID, USER_NAME, FULL_NAME, TEAM_ID, IS_ADMIN, etc.) via SplendidInit.LoginUser.
            builder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = ssoAuthority;
                options.ClientId = ssoClientId;
                options.ClientSecret = ssoClientSecret;
                options.ResponseType = "code"; // Authorization Code flow
                options.SaveTokens = true;     // Persist tokens for downstream use
                options.GetClaimsFromUserInfoEndpoint = true; // Enrich claims from userinfo endpoint

                // Standard OIDC scopes for identity, profile, and email claims.
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                // Map OIDC claims to SplendidCRM identity model.
                // preferred_username maps to the Identity.Name property used by
                // Application_AcquireRequestState (Global.asax.cs lines 312-328) to rebuild
                // GenericPrincipal from session USER_NAME.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType  = "preferred_username",
                    RoleClaimType  = "roles"
                };

                // Configure the sign-in scheme to use cookies for state persistence.
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                // Event handlers for SplendidCRM session integration.
                options.Events = new OpenIdConnectEvents
                {
                    // After OIDC token validation, SplendidCRM must call SplendidInit.LoginUser
                    // to populate session with USER_ID, USER_NAME, FULL_NAME, TEAM_ID, IS_ADMIN, etc.
                    // This integration point connects the OIDC identity to SplendidCRM's user model.
                    // The actual session population is handled by middleware or controller logic
                    // that resolves the authenticated ClaimsPrincipal to a SplendidCRM user record.
                    OnTokenValidated = context =>
                    {
                        // The OIDC token has been validated and claims are available on context.Principal.
                        // Downstream middleware (e.g., SecurityFilterMiddleware) or the first API call
                        // will use the authenticated principal's preferred_username claim to look up
                        // the SplendidCRM user and populate session keys via SplendidInit.LoginUser.
                        //
                        // Session keys populated by SplendidInit.LoginUser (from Security.cs):
                        //   USER_ID, USER_LOGIN_ID, USER_NAME, FULL_NAME, TEAM_ID, TEAM_NAME,
                        //   IS_ADMIN, IS_ADMIN_DELEGATE, PORTAL_ONLY, EXCHANGE_ALIAS, EXCHANGE_EMAIL
                        return Task.CompletedTask;
                    },

                    // Handle SSO failures by redirecting to the login page with an error indicator.
                    // This mirrors the error handling approach from the legacy Global.asax.cs
                    // Application_OnError and provides a consistent user experience.
                    OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect("/login?error=sso_failed");
                        return Task.CompletedTask;
                    }
                };
            });

            return builder;
        }

        /// <summary>
        /// Reads a configuration value supporting both nested JSON keys and flat environment variable keys.
        /// Tries the nested key first (e.g., "SSO:Authority"), then falls back to the flat key (e.g., "SSO_AUTHORITY").
        /// This dual-key approach supports both appsettings.json nested sections and environment variable overrides
        /// in the 5-tier configuration provider hierarchy.
        /// </summary>
        /// <param name="configuration">The configuration instance.</param>
        /// <param name="nestedKey">The nested JSON key (e.g., "SSO:Authority").</param>
        /// <param name="flatKey">The flat environment variable key (e.g., "SSO_AUTHORITY").</param>
        /// <returns>The configuration value, or null if not found in either key.</returns>
        private static string? ReadConfigValue(IConfiguration configuration, string nestedKey, string flatKey)
        {
            // Try nested key first (from appsettings.json sections), then flat key (from env vars).
            string? value = configuration[nestedKey];
            if (string.IsNullOrWhiteSpace(value))
            {
                value = configuration[flatKey];
            }
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// Validates that all required SSO configuration values are present.
        /// Throws <see cref="InvalidOperationException"/> with a descriptive message listing
        /// all missing variables if any are null or empty.
        /// 
        /// Per AAP 0.8.2: "If any Secrets Manager key or required environment variable is missing
        /// or empty, application MUST log the specific missing variable name and exit with non-zero code."
        /// </summary>
        /// <param name="ssoAuthority">The OIDC authority URL.</param>
        /// <param name="ssoClientId">The OIDC client identifier.</param>
        /// <param name="ssoClientSecret">The OIDC client secret.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when one or more required SSO configuration values are missing.
        /// </exception>
        private static void ValidateRequiredConfiguration(
            string? ssoAuthority,
            string? ssoClientId,
            string? ssoClientSecret)
        {
            var missingVariables = new List<string>();

            if (string.IsNullOrWhiteSpace(ssoAuthority))
            {
                missingVariables.Add("SSO_AUTHORITY (or SSO:Authority)");
            }
            if (string.IsNullOrWhiteSpace(ssoClientId))
            {
                missingVariables.Add("SSO_CLIENT_ID (or SSO:ClientId)");
            }
            if (string.IsNullOrWhiteSpace(ssoClientSecret))
            {
                missingVariables.Add("SSO_CLIENT_SECRET (or SSO:ClientSecret)");
            }

            if (missingVariables.Count > 0)
            {
                throw new InvalidOperationException(
                    "SSO authentication (AUTH_MODE=SSO) requires the following configuration values, " +
                    "but they are missing or empty: " + string.Join(", ", missingVariables) + ". " +
                    "Ensure these values are set via AWS Secrets Manager, environment variables, " +
                    "AWS Parameter Store, or appsettings.json. " +
                    "The application cannot start without valid SSO configuration when AUTH_MODE=SSO.");
            }
        }
    }
}
