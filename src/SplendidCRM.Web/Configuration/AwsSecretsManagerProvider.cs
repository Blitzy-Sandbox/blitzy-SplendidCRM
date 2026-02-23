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

// AWS Secrets Manager Configuration Provider
// Replaces hardcoded secrets from legacy Web.config (e.g., SplendidSQLServer connection string on line 5)
// with dynamic retrieval from AWS Secrets Manager using the default credential chain.
//
// This provider is the HIGHEST PRIORITY (1st) in the 5-tier configuration hierarchy:
//   1. AWS Secrets Manager         <-- THIS PROVIDER
//   2. Environment variables
//   3. AWS Systems Manager Parameter Store
//   4. appsettings.{Environment}.json
//   5. appsettings.json
//
// Secrets sourced by this provider:
//   - ConnectionStrings:SplendidCRM   (REQUIRED — fail-fast if missing)
//   - SESSION_CONNECTION              (REQUIRED — fail-fast if missing)
//   - SSO_CLIENT_ID                   (Required if AUTH_MODE=SSO)
//   - SSO_CLIENT_SECRET               (Required if AUTH_MODE=SSO)
//   - DUO_INTEGRATION_KEY             (Optional — 2FA)
//   - DUO_SECRET_KEY                  (Optional — 2FA)
//   - SMTP_CREDENTIALS                (Optional — email sending)
//
// IAM Requirement: ECS Task Role requires kms:Decrypt on CMK + secretsmanager:GetSecretValue

using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;

namespace SplendidCRM.Web.Configuration
{
    /// <summary>
    /// Configuration source that registers the AWS Secrets Manager provider with the
    /// ASP.NET Core configuration pipeline. Implements <see cref="IConfigurationSource"/>
    /// to integrate with <see cref="IConfigurationBuilder"/>.
    /// </summary>
    public class AwsSecretsManagerConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// The AWS Secrets Manager secret name or ARN (e.g., "splendidcrm/production/secrets").
        /// This identifies the secret to retrieve from AWS Secrets Manager.
        /// </summary>
        public string SecretId { get; set; } = string.Empty;

        /// <summary>
        /// When true, the provider will silently return an empty configuration if AWS Secrets Manager
        /// is unavailable or the secret does not exist. When false (default), exceptions propagate
        /// to cause startup failure (fail-fast behavior per AAP Section 0.8.2).
        /// Set to true for local development environments without AWS credentials.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Optional interval for periodic secret reload to support AWS Secrets Manager automatic
        /// rotation. When null (default), secrets are loaded once at startup.
        /// When specified, a background timer re-invokes Load() at this interval to refresh secrets.
        /// </summary>
        public TimeSpan? ReloadInterval { get; set; }

        /// <summary>
        /// Builds the <see cref="AwsSecretsManagerConfigurationProvider"/> for this source.
        /// Called by the ASP.NET Core configuration system during application startup.
        /// </summary>
        /// <param name="builder">The configuration builder (unused by this provider).</param>
        /// <returns>A new <see cref="AwsSecretsManagerConfigurationProvider"/> instance.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new AwsSecretsManagerConfigurationProvider(this);
        }
    }

    /// <summary>
    /// Configuration provider that retrieves secrets from AWS Secrets Manager and
    /// exposes them as ASP.NET Core configuration key-value pairs. Extends
    /// <see cref="ConfigurationProvider"/> to integrate with the IConfiguration system.
    /// 
    /// The provider expects the secret value to be a JSON object with string key-value pairs.
    /// Keys containing double underscores (__) are converted to colons (:) to match
    /// the ASP.NET Core hierarchical configuration key convention.
    /// 
    /// Example secret JSON:
    /// {
    ///   "ConnectionStrings__SplendidCRM": "data source=myserver;initial catalog=SplendidCRM;...",
    ///   "SESSION_CONNECTION": "redis-cluster.abc.cache.amazonaws.com:6379"
    /// }
    /// 
    /// Produces configuration keys:
    ///   "ConnectionStrings:SplendidCRM" => "data source=myserver;..."
    ///   "SESSION_CONNECTION" => "redis-cluster.abc.cache.amazonaws.com:6379"
    /// </summary>
    public class AwsSecretsManagerConfigurationProvider : ConfigurationProvider, IDisposable
    {
        private readonly AwsSecretsManagerConfigurationSource _source;
        private Timer? _reloadTimer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AwsSecretsManagerConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The configuration source containing SecretId, Optional, and ReloadInterval settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public AwsSecretsManagerConfigurationProvider(AwsSecretsManagerConfigurationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Loads (or reloads) secrets from AWS Secrets Manager into the configuration Data dictionary.
        /// 
        /// This method:
        /// 1. Creates an AmazonSecretsManagerClient using the default credential chain
        ///    (IAM role in ECS, environment variables locally, AWS CLI profile as fallback).
        /// 2. Calls GetSecretValueAsync to retrieve the secret identified by SecretId.
        /// 3. Parses the returned SecretString as JSON using System.Text.Json.
        /// 4. Maps each JSON key-value pair into the Data dictionary, replacing __ with :
        ///    for ASP.NET Core hierarchical configuration key convention.
        /// 5. If ReloadInterval is specified and this is the first load, starts a periodic
        ///    timer to re-invoke Load() for secret rotation support.
        /// 
        /// Error handling:
        /// - When Optional=false (default): exceptions propagate, causing startup failure (fail-fast).
        /// - When Optional=true: AWS exceptions are caught and an empty Data dictionary is used,
        ///   allowing the application to start without Secrets Manager (e.g., local development).
        /// 
        /// Thread safety: The base ConfigurationProvider.Data dictionary is replaced atomically.
        /// The synchronous Load() bridges to the async AWS SDK via Task.Run().GetAwaiter().GetResult().
        /// This is acceptable because Load() is called once at startup (or periodically via timer).
        /// </summary>
        public override void Load()
        {
            try
            {
                var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                LoadSecrets(data);
                Data = data;
                InitializeReloadTimer();
            }
            catch (ResourceNotFoundException ex)
            {
                // Secret does not exist in AWS Secrets Manager.
                HandleLoadException(ex, $"AWS Secrets Manager secret '{_source.SecretId}' was not found.");
            }
            catch (DecryptionFailureException ex)
            {
                // KMS decryption failure — likely an IAM permission issue.
                // ECS Task Role requires kms:Decrypt on the CMK used to encrypt the secret.
                HandleLoadException(ex, $"Failed to decrypt AWS Secrets Manager secret '{_source.SecretId}'. " +
                    "Verify that the ECS Task Role has kms:Decrypt permission on the CMK.");
            }
            catch (InvalidRequestException ex)
            {
                // Secret is marked for deletion or in an invalid state.
                HandleLoadException(ex, $"AWS Secrets Manager secret '{_source.SecretId}' is in an invalid state (possibly marked for deletion).");
            }
            catch (AmazonSecretsManagerException ex)
            {
                // General AWS Secrets Manager error — covers authentication failures,
                // network issues, throttling, and other service-level errors.
                HandleLoadException(ex, $"Failed to retrieve secret '{_source.SecretId}' from AWS Secrets Manager. " +
                    $"Error: {ex.Message}");
            }
            catch (Exception ex) when (_source.Optional)
            {
                // Catch-all for unexpected errors (e.g., JSON parsing failures, network timeouts)
                // when the provider is optional (local development without AWS).
                System.Diagnostics.Trace.TraceWarning(
                    $"[AwsSecretsManagerProvider] Optional provider failed for secret '{_source.SecretId}': {ex.Message}");
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Retrieves the secret from AWS Secrets Manager and parses the JSON secret string
        /// into configuration key-value pairs.
        /// </summary>
        /// <param name="data">The dictionary to populate with configuration key-value pairs.</param>
        private void LoadSecrets(Dictionary<string, string?> data)
        {
            // Create the Secrets Manager client using the default credential chain.
            // In ECS: uses the Task Role IAM credentials.
            // In local development: uses environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
            // or the AWS CLI default profile.
            // No hardcoded AWS credentials, regions, or account IDs — all resolved by the SDK.
            using var client = new AmazonSecretsManagerClient();

            var request = new GetSecretValueRequest
            {
                SecretId = _source.SecretId
            };

            // Bridge the async AWS SDK call to synchronous Load() method.
            // This is acceptable as Load() runs at startup or on periodic timer intervals,
            // not in the hot request path.
            var response = Task.Run(async () => await client.GetSecretValueAsync(request).ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();

            if (string.IsNullOrEmpty(response.SecretString))
            {
                // SecretString is null or empty — the secret may be stored as binary (SecretBinary),
                // which is not supported by this provider. Only JSON string secrets are supported.
                System.Diagnostics.Trace.TraceWarning(
                    $"[AwsSecretsManagerProvider] Secret '{_source.SecretId}' has no SecretString value. " +
                    "Only JSON string secrets are supported.");
                return;
            }

            ParseSecretJson(response.SecretString, data);
        }

        /// <summary>
        /// Parses the AWS Secrets Manager JSON secret string and populates the data dictionary.
        /// 
        /// JSON keys containing double underscores (__) are converted to colons (:) to match
        /// the ASP.NET Core hierarchical configuration key convention.
        /// Example: "ConnectionStrings__SplendidCRM" becomes "ConnectionStrings:SplendidCRM"
        /// 
        /// This enables IConfiguration["ConnectionStrings:SplendidCRM"] and
        /// configuration.GetConnectionString("SplendidCRM") to resolve correctly.
        /// </summary>
        /// <param name="secretJson">The JSON string from AWS Secrets Manager.</param>
        /// <param name="data">The dictionary to populate with mapped key-value pairs.</param>
        private static void ParseSecretJson(string secretJson, Dictionary<string, string?> data)
        {
            using var document = JsonDocument.Parse(secretJson);
            var root = document.RootElement;

            foreach (var property in root.EnumerateObject())
            {
                // Apply the ASP.NET Core hierarchical key convention:
                // Replace double underscore (__) with colon (:) in key names.
                // This allows secrets stored as "ConnectionStrings__SplendidCRM" in AWS
                // to be accessed as "ConnectionStrings:SplendidCRM" in IConfiguration.
                string configKey = property.Name.Replace("__", ":", StringComparison.Ordinal);

                // Extract the string value from the JSON property.
                // Non-string JSON values (numbers, booleans, objects, arrays) are converted
                // to their raw JSON text representation to preserve fidelity.
                string? configValue = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText();

                data[configKey] = configValue;
            }
        }

        /// <summary>
        /// Handles exceptions during secret loading based on the Optional flag.
        /// When Optional is true, logs a warning and sets Data to an empty dictionary.
        /// When Optional is false, re-throws to cause startup failure (fail-fast behavior).
        /// </summary>
        /// <param name="ex">The exception that occurred during secret retrieval.</param>
        /// <param name="message">A descriptive message about the failure.</param>
        private void HandleLoadException(Exception ex, string message)
        {
            if (_source.Optional)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[AwsSecretsManagerProvider] {message} Provider is optional; continuing with empty configuration.");
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // Fail-fast: re-throw with enhanced message so the startup validator
                // in Program.cs can detect and report the specific missing secret.
                throw new InvalidOperationException(
                    $"[AwsSecretsManagerProvider] {message} " +
                    "Set Optional=true to allow the application to start without this secret.", ex);
            }
        }

        /// <summary>
        /// Initializes the periodic reload timer for secret rotation support.
        /// Only activated when <see cref="AwsSecretsManagerConfigurationSource.ReloadInterval"/> is specified.
        /// The timer invokes <see cref="Load()"/> at the configured interval to refresh secrets.
        /// </summary>
        private void InitializeReloadTimer()
        {
            // Only initialize the timer once (on the first successful Load call).
            if (_reloadTimer != null || _source.ReloadInterval == null)
            {
                return;
            }

            var reloadInterval = _source.ReloadInterval.Value;

            // Guard against unreasonably short intervals that could cause throttling.
            if (reloadInterval < TimeSpan.FromSeconds(30))
            {
                reloadInterval = TimeSpan.FromSeconds(30);
                System.Diagnostics.Trace.TraceWarning(
                    $"[AwsSecretsManagerProvider] ReloadInterval clamped to minimum of 30 seconds " +
                    "to prevent AWS API throttling.");
            }

            _reloadTimer = new Timer(
                callback: _ => ReloadSecrets(),
                state: null,
                dueTime: reloadInterval,
                period: reloadInterval
            );
        }

        /// <summary>
        /// Timer callback that reloads secrets from AWS Secrets Manager.
        /// Exceptions during reload are caught and logged to prevent
        /// the timer from being terminated by unhandled exceptions.
        /// The application continues running with the previously loaded secrets.
        /// </summary>
        private void ReloadSecrets()
        {
            try
            {
                Load();
                OnReload();
            }
            catch (Exception ex)
            {
                // During periodic reload, never crash the application.
                // Log the error and continue with previously loaded secrets.
                System.Diagnostics.Trace.TraceError(
                    $"[AwsSecretsManagerProvider] Failed to reload secret '{_source.SecretId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Releases resources used by the reload timer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by the reload timer.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(); false if from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _reloadTimer?.Dispose();
                    _reloadTimer = null;
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Extension methods for registering the AWS Secrets Manager configuration provider
    /// with <see cref="IConfigurationBuilder"/>. Provides a fluent API for Program.cs integration.
    /// 
    /// Usage in Program.cs:
    /// <code>
    /// builder.Configuration.AddAwsSecretsManager("splendidcrm/production/secrets");
    /// </code>
    /// </summary>
    public static class AwsSecretsManagerExtensions
    {
        /// <summary>
        /// Adds AWS Secrets Manager as a configuration source. This should be registered as the
        /// LAST source added to <see cref="IConfigurationBuilder"/> because later sources take
        /// precedence in ASP.NET Core's configuration system, making this the highest priority
        /// provider in the 5-tier hierarchy.
        /// 
        /// The provider retrieves a JSON secret from AWS Secrets Manager and maps its key-value
        /// pairs into the ASP.NET Core configuration system. Keys with double underscores (__)
        /// are converted to colons (:) for hierarchical key support.
        /// </summary>
        /// <param name="builder">The configuration builder to add the provider to.</param>
        /// <param name="secretId">
        /// The AWS Secrets Manager secret name or ARN (e.g., "splendidcrm/production/secrets").
        /// </param>
        /// <param name="optional">
        /// When true, the provider silently returns empty configuration if AWS Secrets Manager
        /// is unavailable (for local development without AWS credentials).
        /// When false (default), startup fails if the secret cannot be retrieved.
        /// </param>
        /// <param name="reloadInterval">
        /// Optional interval for periodic secret reload to support AWS Secrets Manager automatic
        /// rotation. Pass null (default) to load secrets only at startup.
        /// </param>
        /// <returns>The <see cref="IConfigurationBuilder"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="secretId"/> is null or empty.</exception>
        public static IConfigurationBuilder AddAwsSecretsManager(
            this IConfigurationBuilder builder,
            string secretId,
            bool optional = false,
            TimeSpan? reloadInterval = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (string.IsNullOrWhiteSpace(secretId))
            {
                throw new ArgumentException("SecretId must not be null or empty.", nameof(secretId));
            }

            builder.Add(new AwsSecretsManagerConfigurationSource
            {
                SecretId = secretId,
                Optional = optional,
                ReloadInterval = reloadInterval
            });

            return builder;
        }
    }
}
