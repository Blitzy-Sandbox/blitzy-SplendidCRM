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
// .NET 10 Migration: This file replaces the legacy IIS <authentication mode="Windows"/>
// configuration from Web.config (line 65) with ASP.NET Core Negotiate authentication.
// The Negotiate scheme supports both Kerberos and NTLM authentication, providing
// cross-platform Windows authentication without IIS dependency (AAP Goal 10).
//
// Legacy source references:
//   - SplendidCRM/_code/Security.cs lines 342-353: IsWindowsAuthentication() detected Windows auth
//     via Request.ServerVariables["AUTH_USER"]. In ASP.NET Core, the Negotiate scheme populates
//     HttpContext.User.Identity.Name with the Windows domain\username instead.
//   - SplendidCRM/Web.config line 65: <authentication mode="Windows"/>
//   - SplendidCRM/Web.config lines 73-80: <authorization><allow users="*"/></authorization>
//     Authorization is handled by SplendidCRM's 4-tier ACL layer, not by the auth scheme.
//   - SplendidCRM/Global.asax.cs lines 312-328: Application_AcquireRequestState set
//     GenericPrincipal/GenericIdentity from Security.USER_NAME. With Negotiate auth, the
//     Windows identity is automatically established by the middleware.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;

namespace SplendidCRM.Web.Authentication
{
    /// <summary>
    /// Provides extension methods to configure Negotiate (Windows/NTLM/Kerberos) authentication
    /// for ASP.NET Core, replacing the legacy IIS <c>&lt;authentication mode="Windows"/&gt;</c>
    /// from Web.config. This is activated when <c>AUTH_MODE=Windows</c> is set in configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the legacy .NET Framework 4.8 application, Windows authentication was handled by IIS
    /// integrated pipeline via <c>&lt;authentication mode="Windows"/&gt;</c> in Web.config.
    /// IIS populated the <c>AUTH_USER</c> server variable, which <c>Security.IsWindowsAuthentication()</c>
    /// checked to detect Windows auth (comparing against <c>USER_NAME</c> to exclude WebParts identity).
    /// </para>
    /// <para>
    /// In ASP.NET Core with Kestrel, the Negotiate authentication scheme replaces this:
    /// <list type="bullet">
    ///   <item>The Negotiate middleware handles the SPNEGO/NTLM challenge-response handshake</item>
    ///   <item>On success, <c>HttpContext.User.Identity.Name</c> contains <c>DOMAIN\username</c></item>
    ///   <item>On failure, a <c>401</c> response with <c>WWW-Authenticate: Negotiate</c> header is returned</item>
    ///   <item>The browser natively handles credential prompting — no login page redirect</item>
    /// </list>
    /// </para>
    /// <para>
    /// NOTE: On Linux, Negotiate authentication requires a valid Kerberos keytab file and
    /// proper krb5.conf configuration. On Windows, it works natively with Active Directory.
    /// See https://learn.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth
    /// </para>
    /// </remarks>
    public static class WindowsAuthenticationSetup
    {
        /// <summary>
        /// Adds Negotiate (Windows/NTLM/Kerberos) authentication to the authentication builder.
        /// This configures the <see cref="NegotiateDefaults.AuthenticationScheme"/> with event
        /// handlers for SplendidCRM integration.
        /// </summary>
        /// <param name="builder">The <see cref="AuthenticationBuilder"/> to configure.</param>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> instance providing access to the 5-tier configuration
        /// hierarchy (AWS Secrets Manager → Env vars → Parameter Store → appsettings.{Env}.json → appsettings.json).
        /// Currently used for future extensibility; the Negotiate scheme itself requires no
        /// application-level configuration beyond scheme registration.
        /// </param>
        /// <returns>The <see cref="AuthenticationBuilder"/> for method chaining.</returns>
        /// <remarks>
        /// <para>
        /// Call this method from <c>Program.cs</c> when <c>AUTH_MODE=Windows</c>:
        /// <code>
        /// var authBuilder = builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme);
        /// authBuilder.AddWindowsAuthentication(builder.Configuration);
        /// </code>
        /// </para>
        /// <para>
        /// This replaces the legacy IIS <c>&lt;authentication mode="Windows"/&gt;</c> from Web.config
        /// line 65. The Negotiate scheme provides the same authentication flow:
        /// <list type="number">
        ///   <item>Client sends initial request without credentials</item>
        ///   <item>Server responds with <c>401</c> and <c>WWW-Authenticate: Negotiate</c> header</item>
        ///   <item>Client (browser) retries with Kerberos ticket or NTLM credentials</item>
        ///   <item>Server validates and establishes <c>WindowsIdentity</c> as <c>HttpContext.User</c></item>
        /// </list>
        /// </para>
        /// <para>
        /// After authentication, SplendidCRM's 4-tier ACL model (Module → Team → Field → Record)
        /// operates on the authenticated principal via <c>ModuleAuthorizationHandler</c> and
        /// <c>Security.Filter()</c> SQL predicate injection, working identically regardless of auth mode.
        /// </para>
        /// </remarks>
        public static AuthenticationBuilder AddWindowsAuthentication(
            this AuthenticationBuilder builder,
            IConfiguration configuration)
        {
            // Register the Negotiate authentication scheme.
            // Negotiate supports both Kerberos (preferred) and NTLM (fallback) authentication.
            // This replaces IIS <authentication mode="Windows"/> from Web.config.
            //
            // NOTE: On Linux, Negotiate auth requires a valid Kerberos keytab.
            // See https://learn.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth
            builder.AddNegotiate(options =>
            {
                // Configure event handlers for SplendidCRM integration.
                // The Negotiate scheme fires these events during the authentication lifecycle.
                options.Events = new NegotiateEvents
                {
                    // OnAuthenticated fires after successful Negotiate handshake.
                    // At this point, context.Principal contains the Windows identity with:
                    //   - context.Principal.Identity.Name = "DOMAIN\username"
                    //   - context.Principal.Identity.AuthenticationType = "Negotiate"
                    //   - context.Principal.Identity.IsAuthenticated = true
                    //
                    // This replaces the legacy AUTH_USER server variable check in
                    // Security.IsWindowsAuthentication() (Security.cs lines 342-353).
                    // In the migrated Security.cs (SplendidCRM.Core), IsWindowsAuthentication()
                    // checks HttpContext.User.Identity.AuthenticationType == "Negotiate" instead
                    // of Request.ServerVariables["AUTH_USER"].
                    //
                    // SplendidCRM maps the Windows identity to a CRM user via SplendidInit.LoginUser,
                    // which populates session keys: USER_ID, USER_NAME, FULL_NAME, TEAM_ID, IS_ADMIN, etc.
                    // That mapping occurs in the request pipeline (middleware/controller layer), not here.
                    OnAuthenticated = context =>
                    {
                        // Authentication succeeded. The Windows identity is now available on the
                        // HttpContext.User principal. Downstream middleware and the SplendidCRM
                        // authorization layer will consume this identity for ACL enforcement.
                        //
                        // No additional processing is needed here — the Negotiate scheme
                        // automatically populates HttpContext.User with the WindowsIdentity.
                        // The migrated Global.asax.cs Application_AcquireRequestState logic
                        // (which set GenericPrincipal from Security.USER_NAME) is no longer needed
                        // because the Negotiate middleware establishes the principal directly.
                        return Task.CompletedTask;
                    },

                    // OnAuthenticationFailed fires when the Negotiate handshake fails.
                    // Common causes include invalid/expired Kerberos tickets, NTLM negotiation
                    // failures, or missing keytab configuration on Linux.
                    //
                    // For API calls to /Rest.svc/ and /Administration/Rest.svc/, the middleware
                    // returns 401 with WWW-Authenticate: Negotiate header, which is the correct
                    // behavior — the client retries with credentials (browser credential prompting).
                    // No redirect to a login page is performed for Windows auth.
                    OnAuthenticationFailed = context =>
                    {
                        // Authentication failed. The default behavior of the Negotiate middleware
                        // is to return a 401 response with the WWW-Authenticate: Negotiate header,
                        // which triggers the browser's native credential dialog.
                        //
                        // This preserves the same client-visible authentication flow as the legacy
                        // IIS Windows auth: 401 challenge → Negotiate handshake → retry with creds.
                        // No custom error handling is needed here; the middleware handles the
                        // challenge-response protocol automatically.
                        return Task.CompletedTask;
                    }
                };
            });

            return builder;
        }
    }
}
