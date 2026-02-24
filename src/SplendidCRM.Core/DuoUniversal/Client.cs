// SPDX-FileCopyrightText: 2022 Cisco Systems, Inc. and/or its affiliates
//
// SPDX-License-Identifier: BSD-3-Clause
// Migrated from SplendidCRM/_code/DuoUniversal/Client.cs for .NET 10

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace DuoUniversal
{
    /// <summary>
    /// DuoUniversal client for performing Duo two-factor authentication via the Universal Prompt.
    /// Preserves the authentication flow for the SplendidCRM integration.
    /// </summary>
    public class Client
    {
        private const string HEALTH_CHECK_ENDPOINT = "/oauth/v1/health_check";
        private const string AUTHORIZE_ENDPOINT = "/oauth/v1/authorize";
        private const string TOKEN_ENDPOINT = "/oauth/v1/token";
        private const int STATE_LENGTH = 36;
        private const int DEFAULT_STATE_LENGTH = 36;

        // Length constants used by Utils.ValidateRequiredParameters for Duo credential validation
        // Preserved from source: SplendidCRM/_code/DuoUniversal/Client.cs
        internal const int CLIENT_ID_LENGTH = 20;
        internal const int CLIENT_SECRET_LENGTH = 40;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _apiHost;
        private readonly string _redirectUri;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Construct a new DuoUniversal Client.
        /// </summary>
        /// <param name="clientId">Duo Integration Key (Client ID)</param>
        /// <param name="clientSecret">Duo Secret Key (Client Secret)</param>
        /// <param name="apiHost">Duo API hostname (e.g. api-XXXXXXXX.duosecurity.com)</param>
        /// <param name="redirectUri">The redirect URI after authentication</param>
        public Client(string clientId, string clientSecret, string apiHost, string redirectUri)
            : this(clientId, clientSecret, apiHost, redirectUri, null)
        {
        }

        /// <summary>
        /// Construct a new DuoUniversal Client with a custom HttpClient.
        /// </summary>
        internal Client(string clientId, string clientSecret, string apiHost, string redirectUri, HttpClient httpClient)
        {
            Utils.ValidateRequiredStringParam(nameof(clientId), clientId);
            Utils.ValidateRequiredStringParam(nameof(clientSecret), clientSecret);
            Utils.ValidateRequiredStringParam(nameof(apiHost), apiHost);
            Utils.ValidateRequiredStringParam(nameof(redirectUri), redirectUri);

            _clientId = clientId;
            _clientSecret = clientSecret;
            _apiHost = apiHost;
            _redirectUri = redirectUri;
            _httpClient = httpClient ?? CreateDefaultHttpClient(apiHost);
        }

        /// <summary>
        /// Create a default HttpClient with Duo certificate pinning.
        /// </summary>
        private static HttpClient CreateDefaultHttpClient(string apiHost)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = CertificatePinnerFactory.GetDuoCertificatePinner()
            };
            return new HttpClient(handler);
        }

        /// <summary>
        /// Generate a random state value for the OIDC flow.
        /// </summary>
        public static string GenerateState()
        {
            return Utils.GenerateRandomString(DEFAULT_STATE_LENGTH);
        }

        /// <summary>
        /// Perform a health check against the Duo API.
        /// </summary>
        public async Task<bool> DoHealthCheck()
        {
            string url = $"https://{_apiHost}{HEALTH_CHECK_ENDPOINT}";
            // Generate a subject JWT for the health check endpoint (minimal claims: iss, aud, jti, exp + sub)
            var healthCheckClaims = new Dictionary<string, string> { { Labels.SUB, _clientId } };
            var parameters = new Dictionary<string, string>
            {
                { Labels.CLIENT_ID, _clientId },
                { Labels.CLIENT_ASSERTION, JwtUtils.CreateSignedJwt(_clientId, _clientSecret, $"https://{_apiHost}{HEALTH_CHECK_ENDPOINT}", healthCheckClaims) },
                { Labels.CLIENT_ASSERTION_TYPE, Labels.JWT_BEARER_TYPE }
            };

            var response = await _httpClient.PostAsync(url, Utils.CreateFormContent(parameters));
            var responseContent = await response.Content.ReadAsStringAsync();
            var healthCheckResponse = JsonSerializer.Deserialize<HealthCheckResponse>(responseContent);

            if (healthCheckResponse?.Stat == "OK")
            {
                return true;
            }

            throw new DuoException($"Duo health check failed: {healthCheckResponse?.Message ?? "Unknown error"}");
        }

        /// <summary>
        /// Create the URL to redirect the user to for Duo authentication.
        /// </summary>
        public string CreateAuthUrl(string username, string state)
        {
            Utils.ValidateRequiredStringParam(nameof(username), username);
            Utils.ValidateRequiredStringParam(nameof(state), state);

            // Build the auth JWT with required OIDC claims for the Duo authorize endpoint
            string authEndpoint = $"https://{_apiHost}{AUTHORIZE_ENDPOINT}";
            var additionalClaims = new Dictionary<string, string>
            {
                { Labels.CLIENT_ID, _clientId },
                { Labels.DUO_UNAME, username },
                { Labels.REDIRECT_URI, _redirectUri },
                { Labels.RESPONSE_TYPE, Labels.CODE },
                { Labels.SCOPE, Labels.OPENID },
                { Labels.STATE, state }
            };
            string jwt = JwtUtils.CreateSignedJwt(_clientId, _clientSecret, authEndpoint, additionalClaims);

            var queryParams = new Dictionary<string, string>
            {
                { Labels.RESPONSE_TYPE, Labels.CODE },
                { Labels.CLIENT_ID, _clientId },
                { Labels.REQUEST, jwt },
                { Labels.REDIRECT_URI, _redirectUri }
            };

            var queryString = new System.Text.StringBuilder();
            bool first = true;
            foreach (var kvp in queryParams)
            {
                if (!first) queryString.Append('&');
                queryString.Append(Uri.EscapeDataString(kvp.Key));
                queryString.Append('=');
                queryString.Append(Uri.EscapeDataString(kvp.Value));
                first = false;
            }

            return $"https://{_apiHost}{AUTHORIZE_ENDPOINT}?{queryString}";
        }

        /// <summary>
        /// Exchange the authorization code from Duo callback for an ID token and validate it.
        /// </summary>
        public async Task<IdToken> ExchangeAuthorizationCodeFor2FAResult(string duoCode, string username)
        {
            Utils.ValidateRequiredStringParam(nameof(duoCode), duoCode);
            Utils.ValidateRequiredStringParam(nameof(username), username);

            string tokenEndpoint = $"https://{_apiHost}{TOKEN_ENDPOINT}";
            // Generate a subject JWT for authenticating the token endpoint request
            var subjectClaims = new Dictionary<string, string> { { Labels.SUB, _clientId } };
            var parameters = new Dictionary<string, string>
            {
                { Labels.GRANT_TYPE, Labels.AUTHORIZATION_CODE },
                { Labels.CODE, duoCode },
                { Labels.REDIRECT_URI, _redirectUri },
                { Labels.CLIENT_ID, _clientId },
                { Labels.CLIENT_ASSERTION, JwtUtils.CreateSignedJwt(_clientId, _clientSecret, tokenEndpoint, subjectClaims) },
                { Labels.CLIENT_ASSERTION_TYPE, Labels.JWT_BEARER_TYPE }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Content = Utils.CreateFormContent(parameters);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new DuoException($"Error exchanging Duo authorization code: {response.StatusCode} - {responseContent}");
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            if (string.IsNullOrEmpty(tokenResponse?.IdToken))
            {
                throw new DuoException("No ID token in Duo token response.");
            }

            // Validate the JWT signature, audience, issuer, and expiry; then decode the token claims
            JwtUtils.ValidateJwt(tokenResponse.IdToken, _clientId, _clientSecret, tokenEndpoint);
            IdToken idToken = Utils.DecodeToken(tokenResponse.IdToken);

            // Enforce that the authenticated username matches the expected username
            if (!string.Equals(idToken.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                throw new DuoException("The specified username does not match the username from Duo");
            }

            return idToken;
        }
    }
}
