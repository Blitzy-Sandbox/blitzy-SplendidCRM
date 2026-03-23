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
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SplendidCRM.Web.Configuration
{
    /// <summary>
    /// Validates ALL required configuration at application startup in Program.cs.
    /// Per AAP Section 0.8.2:
    ///   - If ANY required configuration value is missing or empty, the application MUST
    ///     log the SPECIFIC missing variable name and call Environment.Exit(1) (fail-fast).
    ///   - The application MUST NOT start with null or empty connection strings.
    ///   - Zero configuration values that vary per environment are hardcoded in source.
    ///
    /// This is a NEW requirement — the legacy Global.asax.cs / Web.config codebase did NOT
    /// perform explicit fail-fast validation at startup.
    ///
    /// Configuration Provider Hierarchy (highest priority wins):
    ///   1. AWS Secrets Manager — secrets (ConnectionStrings, SSO credentials, Duo keys, SMTP, Session)
    ///   2. Environment variables — runtime overrides (ASPNETCORE_ENVIRONMENT, SPLENDID_JOB_SERVER, LOG_LEVEL)
    ///   3. AWS Systems Manager Parameter Store — environment-specific non-secret config
    ///   4. appsettings.{Environment}.json — environment defaults
    ///   5. appsettings.json — base defaults
    /// </summary>
    public static class StartupValidator
    {
        // Allowed values for SESSION_PROVIDER enum validation.
        private static readonly HashSet<string> ValidSessionProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Redis",
            "SqlServer"
        };

        // Allowed values for AUTH_MODE enum validation.
        private static readonly HashSet<string> ValidAuthModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Windows",
            "Forms",
            "SSO"
        };

        // Default values for optional interval configurations.
        private const string DefaultSchedulerIntervalMs   = "60000";
        private const string DefaultEmailPollIntervalMs   = "60000";
        private const string DefaultArchiveIntervalMs     = "300000";
        private const string DefaultLogLevel              = "Information";

        /// <summary>
        /// Validates all 18 required, conditionally required, and optional configuration values
        /// specified in AAP Section 0.8.2. Collects all validation errors and performs a single
        /// fail-fast exit if any required configuration is missing or invalid.
        ///
        /// This method is designed to be called from Program.cs before app.Run():
        ///   StartupValidator.Validate(builder.Configuration, logger);
        /// </summary>
        /// <param name="configuration">
        /// The IConfiguration instance providing access to the 5-tier configuration hierarchy.
        /// Uses IConfiguration["KEY"] indexer access and GetConnectionString("SplendidCRM").
        /// </param>
        /// <param name="logger">
        /// The ILogger instance for structured logging. Uses LogError(), LogWarning(), and
        /// LogInformation(). Falls back to Console.Error.WriteLine if logger is null.
        /// </param>
        public static void Validate(IConfiguration configuration, ILogger logger)
        {
            if (configuration == null)
            {
                LogError(logger, "IConfiguration instance is null. Cannot validate startup configuration.");
                Environment.Exit(1);
                return; // Unreachable, but satisfies static analysis.
            }

            List<string> errors = new List<string>();

            // =====================================================================================
            // ALWAYS REQUIRED — FAIL-FAST if missing or empty
            // =====================================================================================

            // 1. ConnectionStrings:SplendidCRM — Database connection string.
            //    Source: AWS Secrets Manager (env var: ConnectionStrings__SplendidCRM).
            //    Application MUST NOT start with null or empty connection strings.
            string? connectionString = configuration.GetConnectionString("SplendidCRM");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                errors.Add("Required configuration 'ConnectionStrings:SplendidCRM' (env var: ConnectionStrings__SplendidCRM) is missing or empty. Source: AWS Secrets Manager.");
            }

            // 2. ASPNETCORE_ENVIRONMENT — Runtime environment identifier.
            //    Source: Environment variable (read directly, not from IConfiguration).
            string? aspnetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (string.IsNullOrWhiteSpace(aspnetEnvironment))
            {
                errors.Add("Required configuration 'ASPNETCORE_ENVIRONMENT' is missing or empty. Source: Environment variable.");
            }

            // 3. SPLENDID_JOB_SERVER — Scheduler job election machine name.
            //    Source: Environment variable. Required for background service coordination.
            string? jobServer = configuration["SPLENDID_JOB_SERVER"];
            if (string.IsNullOrWhiteSpace(jobServer))
            {
                errors.Add("Required configuration 'SPLENDID_JOB_SERVER' is missing or empty. Source: Environment variable.");
            }

            // 4. SESSION_PROVIDER — Distributed session backend selector.
            //    Source: AWS Parameter Store. Must be exactly "Redis" or "SqlServer".
            string? sessionProvider = configuration["SESSION_PROVIDER"];
            if (string.IsNullOrWhiteSpace(sessionProvider))
            {
                errors.Add("Required configuration 'SESSION_PROVIDER' is missing or empty. Must be 'Redis' or 'SqlServer'. Source: AWS Parameter Store.");
            }
            else if (!ValidSessionProviders.Contains(sessionProvider))
            {
                errors.Add($"Required configuration 'SESSION_PROVIDER' has invalid value '{sessionProvider}'. Must be exactly 'Redis' or 'SqlServer'. Source: AWS Parameter Store.");
            }

            // 5. SESSION_CONNECTION — Distributed session backend connection string.
            //    Source: AWS Secrets Manager. FAIL-FAST if missing.
            string? sessionConnection = configuration["SESSION_CONNECTION"];
            if (string.IsNullOrWhiteSpace(sessionConnection))
            {
                errors.Add("Required configuration 'SESSION_CONNECTION' is missing or empty. Source: AWS Secrets Manager.");
            }

            // 6. AUTH_MODE — Authentication mode selector.
            //    Source: AWS Parameter Store. Must be exactly "Windows", "Forms", or "SSO".
            string? authMode = configuration["AUTH_MODE"];
            if (string.IsNullOrWhiteSpace(authMode))
            {
                errors.Add("Required configuration 'AUTH_MODE' is missing or empty. Must be 'Windows', 'Forms', or 'SSO'. Source: AWS Parameter Store.");
            }
            else if (!ValidAuthModes.Contains(authMode))
            {
                errors.Add($"Required configuration 'AUTH_MODE' has invalid value '{authMode}'. Must be exactly 'Windows', 'Forms', or 'SSO'. Source: AWS Parameter Store.");
            }

            // 7. CORS_ORIGINS — Allowed API origins.
            //    Source: AWS Parameter Store. Required.
            string? corsOrigins = configuration["CORS_ORIGINS"];
            if (string.IsNullOrWhiteSpace(corsOrigins))
            {
                errors.Add("Required configuration 'CORS_ORIGINS' is missing or empty. Source: AWS Parameter Store.");
            }

            // =====================================================================================
            // CONDITIONALLY REQUIRED — FAIL-FAST only when AUTH_MODE=SSO
            // =====================================================================================

            if (string.Equals(authMode, "SSO", StringComparison.OrdinalIgnoreCase))
            {
                // SSO_AUTHORITY — OIDC authority URL. Source: AWS Parameter Store. Required when AUTH_MODE=SSO.
                string? ssoAuthority = configuration["SSO_AUTHORITY"];
                if (string.IsNullOrWhiteSpace(ssoAuthority))
                {
                    errors.Add("Required configuration 'SSO_AUTHORITY' is missing or empty (required when AUTH_MODE=SSO). Source: AWS Parameter Store.");
                }

                // SSO_CLIENT_ID — OIDC client ID. Source: AWS Secrets Manager. Required when AUTH_MODE=SSO.
                string? ssoClientId = configuration["SSO_CLIENT_ID"];
                if (string.IsNullOrWhiteSpace(ssoClientId))
                {
                    errors.Add("Required configuration 'SSO_CLIENT_ID' is missing or empty (required when AUTH_MODE=SSO). Source: AWS Secrets Manager.");
                }

                // SSO_CLIENT_SECRET — OIDC client secret. Source: AWS Secrets Manager. Required when AUTH_MODE=SSO.
                string? ssoClientSecret = configuration["SSO_CLIENT_SECRET"];
                if (string.IsNullOrWhiteSpace(ssoClientSecret))
                {
                    errors.Add("Required configuration 'SSO_CLIENT_SECRET' is missing or empty (required when AUTH_MODE=SSO). Source: AWS Secrets Manager.");
                }
            }

            // =====================================================================================
            // OPTIONAL WITH DEFAULTS — Log warning if missing but DO NOT fail
            // =====================================================================================

            // SCHEDULER_INTERVAL_MS — Default: 60000. Source: AWS Parameter Store.
            string? schedulerInterval = configuration["SCHEDULER_INTERVAL_MS"];
            if (string.IsNullOrWhiteSpace(schedulerInterval))
            {
                LogWarning(logger, $"Optional configuration 'SCHEDULER_INTERVAL_MS' is not set. Using default: {DefaultSchedulerIntervalMs}ms.");
            }

            // EMAIL_POLL_INTERVAL_MS — Default: 60000. Source: AWS Parameter Store.
            string? emailPollInterval = configuration["EMAIL_POLL_INTERVAL_MS"];
            if (string.IsNullOrWhiteSpace(emailPollInterval))
            {
                LogWarning(logger, $"Optional configuration 'EMAIL_POLL_INTERVAL_MS' is not set. Using default: {DefaultEmailPollIntervalMs}ms.");
            }

            // ARCHIVE_INTERVAL_MS — Default: 300000. Source: AWS Parameter Store.
            string? archiveInterval = configuration["ARCHIVE_INTERVAL_MS"];
            if (string.IsNullOrWhiteSpace(archiveInterval))
            {
                LogWarning(logger, $"Optional configuration 'ARCHIVE_INTERVAL_MS' is not set. Using default: {DefaultArchiveIntervalMs}ms.");
            }

            // DUO_INTEGRATION_KEY — Optional 2FA. Source: AWS Secrets Manager.
            string? duoIntegrationKey = configuration["DUO_INTEGRATION_KEY"];
            // DUO_SECRET_KEY — Optional 2FA. Source: AWS Secrets Manager.
            string? duoSecretKey = configuration["DUO_SECRET_KEY"];
            // DUO_API_HOSTNAME — Optional 2FA. Source: AWS Parameter Store.
            string? duoApiHostname = configuration["DUO_API_HOSTNAME"];

            // Log informational message about Duo 2FA configuration state.
            bool duoPartiallyConfigured = !string.IsNullOrWhiteSpace(duoIntegrationKey)
                                       || !string.IsNullOrWhiteSpace(duoSecretKey)
                                       || !string.IsNullOrWhiteSpace(duoApiHostname);
            bool duoFullyConfigured = !string.IsNullOrWhiteSpace(duoIntegrationKey)
                                    && !string.IsNullOrWhiteSpace(duoSecretKey)
                                    && !string.IsNullOrWhiteSpace(duoApiHostname);

            if (duoPartiallyConfigured && !duoFullyConfigured)
            {
                // Warn if some Duo values are set but not all — this may indicate a misconfiguration.
                LogWarning(logger, "Duo 2FA is partially configured. All three values (DUO_INTEGRATION_KEY, DUO_SECRET_KEY, DUO_API_HOSTNAME) are required for Duo 2FA to function.");
            }
            else if (!duoPartiallyConfigured)
            {
                LogInformation(logger, "Duo 2FA is not configured. Duo two-factor authentication will be disabled.");
            }

            // SMTP_CREDENTIALS — Optional email sending. Source: AWS Secrets Manager.
            string? smtpCredentials = configuration["SMTP_CREDENTIALS"];
            if (string.IsNullOrWhiteSpace(smtpCredentials))
            {
                LogWarning(logger, "Optional configuration 'SMTP_CREDENTIALS' is not set. Email sending functionality will be unavailable.");
            }

            // LOG_LEVEL — Default: "Information". Source: Environment variable.
            string? logLevel = configuration["LOG_LEVEL"];
            if (string.IsNullOrWhiteSpace(logLevel))
            {
                LogWarning(logger, $"Optional configuration 'LOG_LEVEL' is not set. Using default: {DefaultLogLevel}.");
            }

            // =====================================================================================
            // FAIL-FAST DECISION — Exit with non-zero code if any required config is missing
            // =====================================================================================

            if (errors.Count > 0)
            {
                // Log EACH missing variable as a separate Error-level log entry.
                foreach (string error in errors)
                {
                    LogError(logger, error);
                }

                // Log a summary message indicating total count of validation failures.
                LogError(logger, $"Application startup failed: {errors.Count} required configuration value(s) are missing or invalid. Review the errors above and ensure all required configuration is provided via the appropriate source (AWS Secrets Manager, Environment variables, AWS Parameter Store, or appsettings.json).");

                // Fail-fast with non-zero exit code per AAP Section 0.8.2.
                Environment.Exit(1);
                return; // Unreachable, but satisfies static analysis and testability.
            }

            // All required configuration values validated successfully.
            LogInformation(logger, "All required configuration values validated successfully.");
        }

        /// <summary>
        /// Logs an error message using the provided ILogger, falling back to Console.Error
        /// if the logger is null. This ensures fail-fast error messages are always visible,
        /// even during very early startup before full logging infrastructure is available.
        /// </summary>
        /// <param name="logger">The ILogger instance, or null for console fallback.</param>
        /// <param name="message">The error message to log.</param>
        private static void LogError(ILogger? logger, string message)
        {
            if (logger != null)
            {
                logger.LogError("{Message}", message);
            }
            else
            {
                Console.Error.WriteLine($"[ERROR] {message}");
            }
        }

        /// <summary>
        /// Logs a warning message using the provided ILogger, falling back to Console.Error
        /// if the logger is null.
        /// </summary>
        /// <param name="logger">The ILogger instance, or null for console fallback.</param>
        /// <param name="message">The warning message to log.</param>
        private static void LogWarning(ILogger? logger, string message)
        {
            if (logger != null)
            {
                logger.LogWarning("{Message}", message);
            }
            else
            {
                Console.Error.WriteLine($"[WARNING] {message}");
            }
        }

        /// <summary>
        /// Logs an informational message using the provided ILogger, falling back to Console.Out
        /// if the logger is null.
        /// </summary>
        /// <param name="logger">The ILogger instance, or null for console fallback.</param>
        /// <param name="message">The informational message to log.</param>
        private static void LogInformation(ILogger? logger, string message)
        {
            if (logger != null)
            {
                logger.LogInformation("{Message}", message);
            }
            else
            {
                Console.WriteLine($"[INFO] {message}");
            }
        }
    }
}
