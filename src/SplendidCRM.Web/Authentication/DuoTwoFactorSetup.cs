#nullable disable
// Copyright (C) 2005-2025 SplendidCRM Software, Inc. All Rights Reserved.
// Migration: .NET Framework 4.8 → .NET 10 ASP.NET Core
// DuoUniversal two-factor authentication integration setup.
using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Authentication
{
    /// <summary>
    /// Configures DuoUniversal two-factor authentication for the application.
    /// Reads DUO_INTEGRATION_KEY, DUO_SECRET_KEY, and DUO_API_HOSTNAME from configuration.
    /// DuoUniversal 2FA is optional — only activated if all three configuration values are present.
    /// </summary>
    public static class DuoTwoFactorSetup
    {
        /// <summary>
        /// Registers DuoUniversal 2FA services if DUO configuration is present.
        /// </summary>
        /// <param name="services">The service collection to add DuoUniversal services to.</param>
        /// <param name="configuration">Application configuration containing DUO_* keys.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddDuoTwoFactor(this IServiceCollection services, IConfiguration configuration)
        {
            string integrationKey = configuration["DUO_INTEGRATION_KEY"];
            string secretKey      = configuration["DUO_SECRET_KEY"];
            string apiHostname    = configuration["DUO_API_HOSTNAME"];

            if (!string.IsNullOrEmpty(integrationKey) &&
                !string.IsNullOrEmpty(secretKey) &&
                !string.IsNullOrEmpty(apiHostname))
            {
                // Register DuoUniversal client as a singleton service.
                // The DuoUniversal.Client from SplendidCRM.Core handles HMAC-SHA512 challenge/response
                // and certificate pinning for the Duo API.
                string duoRedirectUri = configuration["DUO_REDIRECT_URI"] ?? "/duo/callback";
                services.AddSingleton(sp =>
                {
                    // Use ClientBuilder (the correct public API for DuoUniversal.Client).
                    // DuoUniversal.Client has an internal constructor — ClientBuilder is the
                    // only way to instantiate it (builder pattern from source file).
                    var client = new DuoUniversal.ClientBuilder(integrationKey, secretKey, apiHostname, duoRedirectUri)
                        .Build();
                    return client;
                });

                // Register a flag service to indicate Duo 2FA is enabled
                services.AddSingleton(new DuoTwoFactorOptions
                {
                    Enabled        = true,
                    IntegrationKey = integrationKey,
                    ApiHostname    = apiHostname
                });
            }
            else
            {
                // Duo 2FA is not configured — register disabled options
                services.AddSingleton(new DuoTwoFactorOptions { Enabled = false });
            }

            return services;
        }
    }

    /// <summary>
    /// Options class indicating whether DuoUniversal 2FA is enabled.
    /// Used by controllers and middleware to conditionally enforce 2FA prompts.
    /// </summary>
    public class DuoTwoFactorOptions
    {
        /// <summary>Whether DuoUniversal 2FA is enabled (all 3 config values present).</summary>
        public bool Enabled { get; set; }

        /// <summary>The Duo integration key (client ID) — non-secret, used for client identification.</summary>
        public string IntegrationKey { get; set; }

        /// <summary>The Duo API hostname for the organization's Duo tenant.</summary>
        public string ApiHostname { get; set; }
    }
}
