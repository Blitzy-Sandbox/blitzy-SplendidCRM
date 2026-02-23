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

// .NET 10 / ASP.NET Core migration: This file replaces the ConfigurationManager.AppSettings pattern
// from Web.config (lines 3-20) with an AWS Systems Manager Parameter Store configuration provider.
// This is the 3rd priority tier in the 5-tier configuration hierarchy:
//   1. AWS Secrets Manager (highest)
//   2. Environment variables
//   3. AWS SSM Parameter Store (this provider)
//   4. appsettings.{Environment}.json
//   5. appsettings.json (lowest)

using Microsoft.Extensions.Configuration;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace SplendidCRM.Web.Configuration
{
    /// <summary>
    /// Configuration source that reads non-secret, environment-specific parameters
    /// from AWS Systems Manager Parameter Store. Implements <see cref="IConfigurationSource"/>
    /// for integration with the ASP.NET Core configuration pipeline.
    ///
    /// Expected parameter path convention:
    ///   /splendidcrm/{environment}/config/{key}
    ///
    /// Parameters loaded by this provider (per AAP 0.8.2):
    ///   SCHEDULER_INTERVAL_MS   — Scheduler hosted service interval (default: 60000)
    ///   EMAIL_POLL_INTERVAL_MS  — Email polling hosted service interval (default: 60000)
    ///   ARCHIVE_INTERVAL_MS     — Archive hosted service interval (default: 300000)
    ///   SESSION_PROVIDER        — Distributed session backend: "Redis" or "SqlServer"
    ///   AUTH_MODE               — Authentication scheme: "Windows", "Forms", or "SSO"
    ///   SSO_AUTHORITY           — OIDC authority URL (required if AUTH_MODE=SSO)
    ///   DUO_API_HOSTNAME        — Duo 2FA API hostname (optional)
    ///   CORS_ORIGINS            — Comma-separated allowed API origins
    ///
    /// NOTE: The provider loads ALL parameters under the base path without filtering.
    /// The list above documents expected keys per the AAP specification.
    /// </summary>
    public class AwsParameterStoreConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// The SSM parameter path prefix used to query parameters.
        /// Example: "/splendidcrm/production/config/"
        /// All parameters under this path (recursively) are loaded into configuration.
        /// </summary>
        public string BasePath { get; set; } = string.Empty;

        /// <summary>
        /// When true, the provider silently returns empty configuration if AWS SSM
        /// Parameter Store is unavailable (e.g., local development without AWS credentials).
        /// When false (default), exceptions propagate to fail-fast during startup validation.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Optional interval for periodic configuration reload from Parameter Store.
        /// When null (default), configuration is loaded once at startup and not refreshed.
        /// </summary>
        public TimeSpan? ReloadInterval { get; set; }

        /// <summary>
        /// Builds the <see cref="AwsParameterStoreConfigurationProvider"/> for this source.
        /// Called by the ASP.NET Core configuration builder during application startup.
        /// </summary>
        /// <param name="builder">The configuration builder (provided by the framework).</param>
        /// <returns>A new <see cref="AwsParameterStoreConfigurationProvider"/> instance.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new AwsParameterStoreConfigurationProvider(this);
        }
    }

    /// <summary>
    /// Configuration provider that retrieves parameters from AWS Systems Manager Parameter Store.
    /// Extends <see cref="ConfigurationProvider"/> and overrides <see cref="Load"/> to fetch
    /// parameters using the AWS SDK default credential chain (IAM role in ECS, environment
    /// variables locally).
    ///
    /// This provider replaces the legacy <c>ConfigurationManager.AppSettings</c> pattern
    /// from Web.config, where non-secret environment-specific values like SplendidProvider,
    /// timer intervals, session provider, and auth mode were previously stored.
    ///
    /// Parameter names are mapped to configuration keys by:
    ///   1. Stripping the base path prefix from the full parameter name
    ///   2. Trimming any leading '/' characters
    ///   3. Replacing remaining '/' separators with ':' (ASP.NET Core key delimiter)
    ///
    /// Example: Parameter "/splendidcrm/production/config/SESSION_PROVIDER"
    ///          with BasePath "/splendidcrm/production/config/"
    ///          yields configuration key "SESSION_PROVIDER"
    /// </summary>
    public class AwsParameterStoreConfigurationProvider : ConfigurationProvider
    {
        private readonly AwsParameterStoreConfigurationSource _source;

        /// <summary>
        /// Initializes a new instance of <see cref="AwsParameterStoreConfigurationProvider"/>.
        /// </summary>
        /// <param name="source">
        /// The configuration source containing the base path, optional flag,
        /// and reload interval settings.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public AwsParameterStoreConfigurationProvider(AwsParameterStoreConfigurationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Loads configuration data from AWS Systems Manager Parameter Store.
        /// This method is called once at application startup by the ASP.NET Core
        /// configuration system. It uses <c>.GetAwaiter().GetResult()</c> to bridge
        /// the synchronous <see cref="ConfigurationProvider.Load"/> contract with
        /// the async AWS SDK, which is acceptable because this executes only during
        /// startup — not on hot request paths.
        ///
        /// The method creates an <see cref="AmazonSimpleSystemsManagementClient"/>
        /// using the AWS default credential chain (IAM role, env vars, AWS profile),
        /// then retrieves all parameters under the configured base path with pagination
        /// support and decryption enabled for SecureString parameters.
        /// </summary>
        public override void Load()
        {
            try
            {
                LoadParametersAsync().GetAwaiter().GetResult();
            }
            catch (AmazonSimpleSystemsManagementException ex)
            {
                if (_source.Optional)
                {
                    // Graceful degradation: when Optional=true, log a warning-level message
                    // and return empty configuration. This supports local development
                    // scenarios where AWS credentials are not available.
                    System.Diagnostics.Trace.TraceWarning(
                        $"AWS SSM Parameter Store is unavailable (Optional=true). " +
                        $"BasePath='{_source.BasePath}', Error='{ex.Message}'. " +
                        $"Continuing with empty parameter store configuration.");
                    Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                // When Optional=false, re-throw to trigger fail-fast startup validation.
                // The startup validator in Program.cs will catch this and report the
                // specific missing configuration provider with a descriptive error message.
                throw;
            }
            catch (AmazonClientException ex)
            {
                // AmazonClientException covers client-side failures including:
                //   - No region endpoint configured (local dev without AWS_REGION)
                //   - Credential resolution failures (no IAM role, no env vars, no profile)
                //   - Network connectivity issues reaching AWS endpoints
                // This is the parent class of AmazonServiceException, so it catches
                // errors that are NOT service-specific (i.e., pre-request failures).
                if (_source.Optional)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"AWS SSM Parameter Store client error (Optional=true). " +
                        $"BasePath='{_source.BasePath}', Error='{ex.Message}'. " +
                        $"Continuing with empty parameter store configuration.");
                    Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                throw;
            }
            catch (Exception ex) when (IsTransientOrCredentialException(ex))
            {
                if (_source.Optional)
                {
                    // Handle non-AWS-specific exceptions that indicate AWS unavailability,
                    // such as HttpRequestException for network issues or TaskCanceledException
                    // for timeouts during local development without AWS infrastructure.
                    System.Diagnostics.Trace.TraceWarning(
                        $"AWS SSM Parameter Store call failed (Optional=true). " +
                        $"BasePath='{_source.BasePath}', Error='{ex.GetType().Name}: {ex.Message}'. " +
                        $"Continuing with empty parameter store configuration.");
                    Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                throw;
            }
        }

        /// <summary>
        /// Asynchronously retrieves all parameters under the configured base path
        /// from AWS SSM Parameter Store, handling pagination transparently.
        /// </summary>
        private async Task LoadParametersAsync()
        {
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            // Use the default credential chain: IAM role (ECS), environment variables,
            // AWS profile, or EC2 instance metadata. No hardcoded credentials.
            using var client = new AmazonSimpleSystemsManagementClient();

            string? nextToken = null;

            do
            {
                var request = new GetParametersByPathRequest
                {
                    Path           = _source.BasePath,
                    Recursive      = true,
                    WithDecryption = true,
                    NextToken      = nextToken
                };

                // Execute the paginated request. Each response may contain up to
                // 10 parameters (AWS default page size). Continue until NextToken is empty.
                var response = await client.GetParametersByPathAsync(request).ConfigureAwait(false);

                if (response.Parameters != null)
                {
                    foreach (var parameter in response.Parameters)
                    {
                        // Map SSM parameter name to ASP.NET Core configuration key:
                        //   1. Strip the base path prefix
                        //   2. Trim leading '/' characters
                        //   3. Replace remaining '/' with ':' (ASP.NET Core hierarchical key separator)
                        //
                        // Example:
                        //   Parameter.Name = "/splendidcrm/production/config/SESSION_PROVIDER"
                        //   BasePath       = "/splendidcrm/production/config/"
                        //   Result key     = "SESSION_PROVIDER"
                        //
                        // For nested parameters:
                        //   Parameter.Name = "/splendidcrm/production/config/Logging/Level"
                        //   Result key     = "Logging:Level"
                        string key = parameter.Name;

                        if (key.StartsWith(_source.BasePath, StringComparison.OrdinalIgnoreCase))
                        {
                            key = key.Substring(_source.BasePath.Length);
                        }

                        key = key.TrimStart('/');
                        key = key.Replace('/', ':');

                        if (!string.IsNullOrEmpty(key))
                        {
                            data[key] = parameter.Value;
                        }
                    }
                }

                nextToken = response.NextToken;

            } while (!string.IsNullOrEmpty(nextToken));

            // Atomically replace the Data dictionary. The base class Data property
            // is thread-safe for read access after Load() completes.
            Data = data;
        }

        /// <summary>
        /// Determines whether an exception represents a transient failure or
        /// AWS credential resolution failure that should be treated as "AWS unavailable"
        /// when the provider is configured as optional.
        /// </summary>
        /// <param name="ex">The exception to evaluate.</param>
        /// <returns>True if the exception indicates transient/credential failure.</returns>
        private static bool IsTransientOrCredentialException(Exception ex)
        {
            // Amazon SDK credential resolution failures surface as various exception types
            // depending on the environment (e.g., AmazonClientException, HttpRequestException,
            // TaskCanceledException for timeouts). We treat all non-AmazonSimpleSystemsManagement
            // exceptions as potentially transient/credential-related when Optional=true.
            // AmazonSimpleSystemsManagementException is handled separately in Load().
            return ex is HttpRequestException
                || ex is TaskCanceledException
                || ex is OperationCanceledException
                || ex is Amazon.Runtime.AmazonServiceException
                || (ex is AggregateException agg && agg.InnerExceptions.Any(
                       inner => IsTransientOrCredentialException(inner)));
        }
    }

    /// <summary>
    /// Extension methods for registering the AWS SSM Parameter Store configuration
    /// provider with the ASP.NET Core <see cref="IConfigurationBuilder"/>.
    ///
    /// Usage in Program.cs:
    /// <code>
    /// builder.Configuration.AddAwsParameterStore(
    ///     basePath: $"/splendidcrm/{builder.Environment.EnvironmentName}/config/",
    ///     optional: builder.Environment.IsDevelopment()
    /// );
    /// </code>
    /// </summary>
    public static class AwsParameterStoreExtensions
    {
        /// <summary>
        /// Adds AWS Systems Manager Parameter Store as a configuration source.
        /// Parameters are retrieved from the specified <paramref name="basePath"/> recursively,
        /// with SecureString decryption enabled. Parameter names are mapped to configuration
        /// keys by stripping the base path and replacing '/' with ':'.
        /// </summary>
        /// <param name="builder">The configuration builder to extend.</param>
        /// <param name="basePath">
        /// The SSM parameter path prefix. All parameters under this path (recursively)
        /// are loaded into the configuration system.
        /// Example: "/splendidcrm/production/config/"
        /// </param>
        /// <param name="optional">
        /// When true, the provider silently returns empty configuration if Parameter Store
        /// is unavailable. When false (default), startup fails fast with a descriptive error.
        /// Set to true for local development environments without AWS credentials.
        /// </param>
        /// <returns>The <see cref="IConfigurationBuilder"/> for method chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> or <paramref name="basePath"/> is null.
        /// </exception>
        public static IConfigurationBuilder AddAwsParameterStore(
            this IConfigurationBuilder builder,
            string basePath,
            bool optional = false)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new ArgumentNullException(nameof(basePath));
            }

            builder.Add(new AwsParameterStoreConfigurationSource
            {
                BasePath = basePath,
                Optional = optional
            });

            return builder;
        }
    }
}
