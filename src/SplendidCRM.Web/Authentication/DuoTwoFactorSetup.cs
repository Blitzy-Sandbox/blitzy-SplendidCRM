#nullable disable
// Copyright (C) 2005-2025 SplendidCRM Software, Inc. All Rights Reserved.
// Migration: .NET Framework 4.8 → .NET 10 ASP.NET Core
//
// Source: SplendidCRM/_code/DuoUniversal/Client.cs (ClientBuilder fluent API — lines 349-627)
// Source: SplendidCRM/_code/Security.cs (authentication facade; Duo 2FA is part of the auth flow)
// Source: SplendidCRM/Web.config (lines 159-171 — binding redirects for
//         Microsoft.IdentityModel.JsonWebTokens 6.34.0.0, a DuoUniversal transitive dependency;
//         assembly binding redirects are eliminated in .NET 10 — NuGet resolves versioning)
//
// Change summary vs. legacy code:
//   - Wires DuoUniversal.Client (preserved in SplendidCRM.Core/DuoUniversal/Client.cs) into the
//     ASP.NET Core DI container as a singleton service, replacing any manual instantiation.
//   - Conditional enablement: 2FA only activated when DUO_INTEGRATION_KEY is present.
//   - Supports both flat env-var key names (DUO_INTEGRATION_KEY) and nested JSON config keys
//     (Duo:IntegrationKey), matching the 5-tier configuration provider hierarchy from Program.cs.
//   - No System.Web references. No IIS/OWIN dependencies.
//   - HMAC SHA-512 signing and certificate pinning are preserved inside DuoUniversal.Client
//     in SplendidCRM.Core — this setup file does NOT modify those implementations.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DuoUniversal;

namespace SplendidCRM.Web.Authentication
{
    /// <summary>
    /// Provides DI registration for DuoUniversal two-factor authentication.
    ///
    /// Called from Program.cs during service registration:
    ///   <code>builder.Services.AddDuoTwoFactorAuthentication(builder.Configuration);</code>
    ///
    /// Conditional enablement rules (per AAP 0.8.2):
    ///   - If DUO_INTEGRATION_KEY is absent → Duo 2FA disabled, no DuoUniversal.Client registered.
    ///   - If DUO_INTEGRATION_KEY is present but DUO_SECRET_KEY or DUO_API_HOSTNAME is missing
    ///     → warning logged, Duo 2FA disabled (graceful degradation).
    ///   - If all three values are present → DuoUniversal.Client registered as a singleton.
    ///
    /// The <see cref="DuoTwoFactorOptions"/> singleton is always registered so that downstream
    /// controllers and middleware can check <see cref="DuoTwoFactorOptions.Enabled"/> without
    /// needing a null-check on <see cref="Client"/>.
    ///
    /// Migration note: DuoUniversal.Client uses an internal constructor — ClientBuilder is the
    /// only public way to construct it (builder pattern). ClientBuilder.Build() calls
    /// Utils.ValidateRequiredParameters() and configures certificate pinning (DuoCertificatePinner)
    /// for HTTPS connections to the Duo API. These behaviors are unchanged from the source library.
    /// </summary>
    public static class DuoTwoFactorSetup
    {
        /// <summary>
        /// Registers DuoUniversal 2FA services if DUO configuration is present.
        /// </summary>
        /// <param name="services">
        /// The service collection to add DuoUniversal services to.
        /// </param>
        /// <param name="configuration">
        /// Application configuration providing access to the 5-tier configuration hierarchy.
        /// Reads: DUO_INTEGRATION_KEY (flat env var) or Duo:IntegrationKey (nested JSON),
        ///        DUO_SECRET_KEY (flat env var) or Duo:SecretKey (nested JSON),
        ///        DUO_API_HOSTNAME (flat env var) or Duo:ApiHostname (nested JSON),
        ///        Duo:RedirectUri or DUO_REDIRECT_URI (redirect URI, default: /duo/callback).
        /// </param>
        /// <returns>The service collection for method chaining.</returns>
        public static IServiceCollection AddDuoTwoFactorAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // ---------------------------------------------------------------------------
            // Step 1: Read DUO_INTEGRATION_KEY — the primary enablement gate.
            // Per AAP 0.8.2 env var table: optional. Sourced from AWS Secrets Manager.
            // Supports both flat env var name (DUO_INTEGRATION_KEY) used by all environments,
            // and nested JSON key (Duo:IntegrationKey) used in appsettings.*.json for dev overrides.
            // If absent, Duo 2FA is simply not enabled — no exception, no services registered.
            // ---------------------------------------------------------------------------
            string integrationKey = configuration["DUO_INTEGRATION_KEY"]
                                 ?? configuration["Duo:IntegrationKey"];

            if (string.IsNullOrEmpty(integrationKey))
            {
                // Duo 2FA is not configured. Per AAP 0.8.2, DUO_INTEGRATION_KEY is optional.
                // Register DuoTwoFactorOptions with Enabled=false so that consuming code
                // (controllers, middleware) can check the flag without null-checking Client.
                services.AddSingleton(new DuoTwoFactorOptions { Enabled = false });
                return services;
            }

            // ---------------------------------------------------------------------------
            // Step 2: Read remaining required configuration values.
            // DUO_SECRET_KEY: Per AAP 0.8.2 — optional, sourced from AWS Secrets Manager.
            // DUO_API_HOSTNAME: Per AAP 0.8.2 — optional, sourced from AWS Parameter Store.
            // Both are required together with DUO_INTEGRATION_KEY to instantiate the client.
            // ---------------------------------------------------------------------------
            string secretKey   = configuration["DUO_SECRET_KEY"]  ?? configuration["Duo:SecretKey"];
            string apiHostname = configuration["DUO_API_HOSTNAME"] ?? configuration["Duo:ApiHostname"];

            // ---------------------------------------------------------------------------
            // Step 3: Acquire a logger via a temporary service provider.
            // Standard pattern for logging inside IServiceCollection extension methods before
            // the application host is fully built. The provider is disposed after use.
            // ILoggerFactory is registered by default in ASP.NET Core host builder setups;
            // if not yet registered (e.g. minimal test scenarios), logger will be null and
            // logging calls are safely skipped via null-conditional operators.
            // ---------------------------------------------------------------------------
            using var temporaryProvider = services.BuildServiceProvider();
            var loggerFactory           = temporaryProvider.GetService<ILoggerFactory>();
            var logger                  = loggerFactory?.CreateLogger(nameof(DuoTwoFactorSetup));

            // ---------------------------------------------------------------------------
            // Step 4: Graceful degradation — partial configuration detected.
            // If DUO_INTEGRATION_KEY is present but either DUO_SECRET_KEY or DUO_API_HOSTNAME
            // is missing, log a warning and skip registration. This prevents a cryptic
            // DuoException from Utils.ValidateRequiredParameters() at DI resolution time.
            // The application continues to run without 2FA rather than failing at startup.
            // ---------------------------------------------------------------------------
            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(apiHostname))
            {
                string missingKey = string.IsNullOrEmpty(secretKey)
                    ? "DUO_SECRET_KEY"
                    : "DUO_API_HOSTNAME";

                logger?.LogWarning(
                    "DuoUniversal 2FA partially configured - missing {MissingKey}. " +
                    "DuoUniversal 2FA will not be enabled. " +
                    "Provide DUO_INTEGRATION_KEY, DUO_SECRET_KEY, and DUO_API_HOSTNAME " +
                    "to enable two-factor authentication.",
                    missingKey);

                // Register disabled options so consuming code can check DuoTwoFactorOptions.Enabled.
                services.AddSingleton(new DuoTwoFactorOptions { Enabled = false });
                return services;
            }

            // ---------------------------------------------------------------------------
            // Step 5: Read the redirect URI for the Duo OAuth 2.0 callback.
            // After the user completes 2FA on the Duo prompt page, Duo redirects back to
            // this URI with an authorization code. The actual callback handling (code exchange
            // for IdToken via Client.ExchangeAuthorizationCodeFor2faResult) is performed by
            // the controller layer, not by this setup file.
            // Per AAP 0.8.2, this URI is configurable via "Duo:RedirectUri" or
            // "DUO_REDIRECT_URI" environment variable, with fallback to "/duo/callback".
            // ---------------------------------------------------------------------------
            string redirectUri = configuration["Duo:RedirectUri"]
                               ?? configuration["DUO_REDIRECT_URI"]
                               ?? "/duo/callback";

            // ---------------------------------------------------------------------------
            // Step 6: Build the DuoUniversal.Client using the ClientBuilder fluent API.
            //
            // DuoUniversal.Client has an internal constructor — ClientBuilder is the ONLY
            // public factory method. This pattern is from the original source library
            // (SplendidCRM/_code/DuoUniversal/Client.cs, lines 349-515).
            //
            // ClientBuilder constructor parameters (all required):
            //   clientId     = DUO_INTEGRATION_KEY  — Duo application/integration identifier
            //   clientSecret = DUO_SECRET_KEY        — HMAC-SHA512 signing key for JWT assertions
            //   apiHost      = DUO_API_HOSTNAME      — Duo API endpoint hostname for the org tenant
            //   redirectUri  = Duo:RedirectUri       — OAuth callback URI post-2FA
            //
            // ClientBuilder.Build() internally calls:
            //   1. Utils.ValidateRequiredParameters() — validates non-null, non-empty params and
            //      CLIENT_ID_LENGTH (20) / CLIENT_SECRET_LENGTH (40) length constraints
            //   2. BuildHttpClient() → GetMessageHandler() → GetCertificatePinner()
            //      — sets up CertificatePinnerFactory.GetDuoCertificatePinner() for Duo root cert
            //        validation (certificate pinning preserved from source library, per AAP 0.8.1)
            //   3. AddUserAgent() — adds duo_universal_csharp/1.3.1 + OS info to User-Agent header
            //
            // The built Client supports:
            //   - DoHealthCheck() — verifies Duo API availability
            //   - GenerateAuthUri() — generates redirect URI for Duo 2FA prompt
            //   - ExchangeAuthorizationCodeFor2faResult() — exchanges auth code for IdToken
            // ---------------------------------------------------------------------------
            Client duoClient = new ClientBuilder(
                clientId:     integrationKey,
                clientSecret: secretKey,
                apiHost:      apiHostname,
                redirectUri:  redirectUri
            ).Build();

            // Register the built DuoUniversal.Client as a singleton.
            // Controllers and services that need to initiate or validate 2FA inject Client via DI:
            //   public MyController(DuoUniversal.Client duoClient) { ... }
            services.AddSingleton(duoClient);

            // Register DuoTwoFactorOptions with Enabled=true for consuming code that needs
            // to conditionally enforce 2FA prompts without directly depending on Client.
            services.AddSingleton(new DuoTwoFactorOptions
            {
                Enabled        = true,
                IntegrationKey = integrationKey,
                ApiHostname    = apiHostname
            });

            // Log successful registration.
            logger?.LogInformation(
                "DuoUniversal 2FA integration enabled for host {ApiHostname}",
                apiHostname);

            return services;
        }
    }

    /// <summary>
    /// Options singleton indicating whether DuoUniversal 2FA is enabled for this application
    /// instance. Always registered by <see cref="DuoTwoFactorSetup.AddDuoTwoFactorAuthentication"/>,
    /// with <see cref="Enabled"/> set to <c>true</c> only when all required Duo configuration
    /// values (DUO_INTEGRATION_KEY, DUO_SECRET_KEY, DUO_API_HOSTNAME) are present and valid.
    ///
    /// Usage in controllers:
    /// <code>
    /// public MyController(DuoTwoFactorOptions duoOptions) {
    ///     if (duoOptions.Enabled) { /* enforce 2FA */ }
    /// }
    /// </code>
    /// </summary>
    public class DuoTwoFactorOptions
    {
        /// <summary>
        /// Whether DuoUniversal 2FA is enabled (all 3 required configuration values present and
        /// DuoUniversal.Client successfully registered as a singleton service).
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The Duo integration key (client ID) used to identify the Duo application.
        /// Non-secret. Only populated when <see cref="Enabled"/> is <c>true</c>.
        /// </summary>
        public string IntegrationKey { get; set; }

        /// <summary>
        /// The Duo API hostname for this organization's Duo tenant
        /// (e.g., api-xxxxxxxx.duosecurity.com). Only populated when <see cref="Enabled"/>
        /// is <c>true</c>.
        /// </summary>
        public string ApiHostname { get; set; }
    }
}
