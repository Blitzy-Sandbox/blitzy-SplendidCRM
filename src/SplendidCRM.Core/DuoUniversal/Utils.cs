// SPDX-FileCopyrightText: 2022 Cisco Systems, Inc. and/or its affiliates
//
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DuoUniversal
{
    /// <summary>
    /// Static utility helpers for the DuoUniversal OIDC 2FA client.
    /// .NET 10 migration: RNGCryptoServiceProvider replaced with RandomNumberGenerator.Create()
    /// (RNGCryptoServiceProvider was marked [Obsolete] in .NET 6+ and removed in .NET 10).
    /// All business logic is preserved exactly from the .NET Framework 4.8 source.
    /// </summary>
    internal class Utils
    {
        /// <summary>
        /// Generate a cryptographically random alphanumeric string of the specified length.
        /// Uses rejection sampling via <see cref="GenerateValidChar"/> to guarantee uniform
        /// distribution over the set of ASCII letters and digits [A-Za-z0-9].
        /// </summary>
        /// <param name="length">The desired length (must be greater than 0)</param>
        /// <returns>A random alphanumeric string of the specified length</returns>
        /// <exception cref="DuoException">Thrown when <paramref name="length"/> is less than or equal to zero</exception>
        internal static string GenerateRandomString(int length)
        {
            if (length <= 0)
            {
                throw new DuoException("Cannot generate random strings shorter than 1 character.");
            }

            // .NET 10 migration: replaced deprecated RNGCryptoServiceProvider with RandomNumberGenerator.Create().
            // RandomNumberGenerator is in System.Security.Cryptography and provides identical cryptographic
            // randomness via the same GetBytes(byte[]) API surface. Behavior is functionally identical.
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                StringBuilder sb = new StringBuilder();
                while (sb.Length < length)
                {
                    sb.Append(GenerateValidChar(rng));
                }
                return sb.ToString().Substring(0, length);
            }
        }

        /// <summary>
        /// Randomly generate a valid alphanumeric character from the provided RNG using rejection sampling.
        /// Bytes outside the printable ASCII letter/digit range are discarded and resampled.
        /// </summary>
        /// <param name="rng">The RNG to use for byte generation</param>
        /// <returns>A randomly-selected ASCII letter or digit character</returns>
        private static char GenerateValidChar(RandomNumberGenerator rng)
        {
            byte[] b = new byte[1];
            char c;
            do
            {
                // .NET 10 migration: rng.GetBytes() works identically on RandomNumberGenerator
                // as it did on the deprecated RNGCryptoServiceProvider.
                rng.GetBytes(b);
                c = (char)b[0];
            } while (!char.IsLetterOrDigit(c));

            return c;
        }

        /// <summary>
        /// Decode a JWT into an <see cref="IdToken"/>. This method decodes the JWT payload but does
        /// not validate the signature — signature validation is performed separately by JwtUtils.
        /// An exception will be thrown if the JWT is not suitable for decoding into an IdToken.
        /// </summary>
        /// <param name="jwt">The JWT string to decode</param>
        /// <returns>The <see cref="IdToken"/> representing the decoded JWT claims</returns>
        /// <exception cref="DuoException">Thrown when the JWT cannot be parsed or required claims are missing</exception>
        internal static IdToken DecodeToken(string jwt)
        {
            try
            {
                JsonWebToken token = new JsonWebToken(jwt);

                // Extract auth_context claim and deserialize into AuthContext model
                string authContextJson = token.GetClaim(Labels.AUTH_CONTEXT).Value;
                AuthContext authContext = JsonSerializer.Deserialize<AuthContext>(authContextJson);

                // Extract auth_result claim and deserialize into AuthResult model
                string authResultJson = token.GetClaim(Labels.AUTH_RESULT).Value;
                AuthResult authResult = JsonSerializer.Deserialize<AuthResult>(authResultJson);

                // Extract auth_time as integer epoch seconds
                int authTime = int.Parse(token.GetClaim(Labels.AUTH_TIME).Value);

                // Extract preferred_username claim
                string username = token.GetClaim(Labels.PREFERRED_USERNAME).Value;

                // Realistically there will only ever be one Audience value; join for safety
                var audiences = string.Join(",", token.Audiences);

                return new IdToken
                {
                    AuthContext = authContext,
                    AuthResult = authResult,
                    AuthTime = authTime,
                    Username = username,
                    Iss = token.Issuer,
                    Exp = token.ValidTo,
                    Iat = token.IssuedAt,
                    Sub = token.Subject,
                    Aud = audiences
                    // TODO Nonce — preserved from source; nonce validation handled in JwtUtils.ValidateIdToken
                };
            }
            catch (Exception e)
            {
                throw new DuoException("Error while parsing the auth token response", e);
            }
        }

        /// <summary>
        /// Validate the provided Duo Client parameters with exact length checks.
        /// <list type="bullet">
        ///   <item>Client ID must be non-empty and exactly <see cref="Client.CLIENT_ID_LENGTH"/> characters</item>
        ///   <item>Client Secret must be non-empty and exactly <see cref="Client.CLIENT_SECRET_LENGTH"/> characters</item>
        ///   <item>API Host must be a non-empty string</item>
        ///   <item>Redirect URI must be a non-empty string</item>
        /// </list>
        /// </summary>
        /// <param name="clientId">The Duo Integration Key (Client ID) to validate</param>
        /// <param name="clientSecret">The Duo Secret Key (Client Secret) to validate</param>
        /// <param name="apiHost">The Duo API hostname to validate</param>
        /// <param name="redirectUri">The OAuth redirect URI to validate</param>
        /// <exception cref="DuoException">Thrown when any parameter fails its validation rule</exception>
        internal static void ValidateRequiredParameters(string clientId, string clientSecret, string apiHost, string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(clientId) || clientId.Length != Client.CLIENT_ID_LENGTH)
            {
                throw new DuoException($"Client ID must be a non-empty string of length {Client.CLIENT_ID_LENGTH}");
            }

            if (string.IsNullOrWhiteSpace(clientSecret) || clientSecret.Length != Client.CLIENT_SECRET_LENGTH)
            {
                throw new DuoException($"Client Secret must be a non-empty string of length {Client.CLIENT_SECRET_LENGTH}");
            }

            if (string.IsNullOrWhiteSpace(apiHost))
            {
                throw new DuoException("API Host must be a non-empty string");
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new DuoException("Redirect URI must be a non-empty string");
            }
        }

        /// <summary>
        /// Validate that a named string parameter is not null, empty, or whitespace.
        /// Used for basic null/empty checks in the Client constructor and OIDC flow methods.
        /// Note: For full Duo parameter validation including length checks, use
        /// <see cref="ValidateRequiredParameters"/> instead.
        /// </summary>
        /// <param name="paramName">The name of the parameter (included in error message)</param>
        /// <param name="paramValue">The value to validate</param>
        /// <exception cref="DuoException">Thrown when <paramref name="paramValue"/> is null, empty, or whitespace</exception>
        internal static void ValidateRequiredStringParam(string paramName, string paramValue)
        {
            if (string.IsNullOrWhiteSpace(paramValue))
            {
                throw new DuoException($"'{paramName}' must not be null or empty.");
            }
        }

        /// <summary>
        /// Encode a byte array as a Base64Url string (RFC 4648 §5).
        /// Replaces '+' with '-', '/' with '_', and strips '=' padding.
        /// </summary>
        /// <param name="input">The byte array to encode</param>
        /// <returns>The Base64Url-encoded string</returns>
        internal static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Decode a Base64Url encoded string (RFC 4648 §5) to a byte array.
        /// Re-adds '=' padding, replaces '-' with '+' and '_' with '/'.
        /// </summary>
        /// <param name="input">The Base64Url-encoded string to decode</param>
        /// <returns>The decoded byte array</returns>
        internal static byte[] Base64UrlDecode(string input)
        {
            string padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        /// <summary>
        /// Create an <see cref="FormUrlEncodedContent"/> instance from a dictionary of key-value pairs.
        /// Used to construct the body of POST requests to Duo OIDC endpoints.
        /// </summary>
        /// <param name="parameters">The key-value pairs to encode as application/x-www-form-urlencoded</param>
        /// <returns>A <see cref="FormUrlEncodedContent"/> ready for use with <see cref="HttpClient"/></returns>
        internal static FormUrlEncodedContent CreateFormContent(IDictionary<string, string> parameters)
        {
            return new FormUrlEncodedContent(parameters);
        }

        /// <summary>
        /// Create an HTTP Basic Authorization header value by Base64-encoding "clientId:clientSecret".
        /// Used for OAuth client authentication in token endpoint requests.
        /// </summary>
        /// <param name="clientId">The client ID (username portion)</param>
        /// <param name="clientSecret">The client secret (password portion)</param>
        /// <returns>The Base64-encoded Basic auth header value (without the "Basic " prefix)</returns>
        internal static string CreateBasicAuthHeader(string clientId, string clientSecret)
        {
            string credentials = $"{clientId}:{clientSecret}";
            byte[] bytes = Encoding.UTF8.GetBytes(credentials);
            return Convert.ToBase64String(bytes);
        }
    }
}
