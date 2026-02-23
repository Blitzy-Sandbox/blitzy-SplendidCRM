// SPDX-FileCopyrightText: 2022 Cisco Systems, Inc. and/or its affiliates
//
// SPDX-License-Identifier: BSD-3-Clause
// Migrated from SplendidCRM/_code/DuoUniversal/JwtUtils.cs for .NET 10

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DuoUniversal
{
    /// <summary>
    /// JWT utility methods for creating and validating JWTs used in the Duo OIDC flow.
    /// </summary>
    internal static class JwtUtils
    {
        private const int JWT_EXPIRATION_SECONDS = 300;

        /// <summary>
        /// Create a signed JWT for authenticating with the Duo API.
        /// Uses HMAC SHA-512 signing with the client secret.
        /// </summary>
        internal static string CreateJwt(string clientId, string clientSecret, string audience, string duoUsername, string nonce)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clientSecret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

            var now = DateTime.UtcNow;
            var claims = new Dictionary<string, object>
            {
                { Labels.CLIENT_ID, clientId },
                { "sub", clientId },
                { "jti", Utils.GenerateRandomString(36) }
            };

            if (!string.IsNullOrEmpty(duoUsername))
            {
                claims["duo_uname"] = duoUsername;
            }

            if (!string.IsNullOrEmpty(nonce))
            {
                claims["nonce"] = nonce;
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = clientId,
                Audience = audience,
                Expires = now.AddSeconds(JWT_EXPIRATION_SECONDS),
                IssuedAt = now,
                NotBefore = now,
                SigningCredentials = credentials,
                Claims = claims
            };

            var handler = new JsonWebTokenHandler();
            return handler.CreateToken(descriptor);
        }

        /// <summary>
        /// Validate an ID token received from Duo.
        /// Verifies signature, issuer, audience, expiration, nonce, and preferred_username.
        /// </summary>
        internal static IdToken ValidateIdToken(string idToken, string clientId, string clientSecret,
            string expectedIssuer, string expectedUsername, string expectedNonce)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clientSecret));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = expectedIssuer,
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                IssuerSigningKey = securityKey,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha512 },
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var handler = new JsonWebTokenHandler();
            var result = handler.ValidateTokenAsync(idToken, validationParameters).GetAwaiter().GetResult();

            if (!result.IsValid)
            {
                throw new DuoException($"Error validating Duo ID token: {result.Exception?.Message}");
            }

            var token = result.SecurityToken as JsonWebToken;
            if (token == null)
            {
                throw new DuoException("ID token could not be parsed.");
            }

            // Validate nonce
            if (!string.IsNullOrEmpty(expectedNonce))
            {
                string actualNonce = GetClaimValue(token, "nonce");
                if (actualNonce != expectedNonce)
                {
                    throw new DuoException("ID token nonce does not match expected nonce.");
                }
            }

            // Validate preferred_username
            string preferredUsername = GetClaimValue(token, "preferred_username");
            if (!string.IsNullOrEmpty(expectedUsername) && !string.Equals(preferredUsername, expectedUsername, StringComparison.OrdinalIgnoreCase))
            {
                throw new DuoException("ID token preferred_username does not match expected username.");
            }

            // Extract auth_context from the token if present
            var authContext = new AuthContext();
            string authCtxJson = GetClaimValue(token, "auth_context");
            if (!string.IsNullOrEmpty(authCtxJson))
            {
                try
                {
                    authContext = JsonSerializer.Deserialize<AuthContext>(authCtxJson) ?? new AuthContext();
                }
                catch
                {
                    // If deserialization fails, use default empty AuthContext
                }
            }
            else
            {
                authContext.Result = GetClaimValue(token, "auth_result");
                authContext.Reason = GetClaimValue(token, "auth_reason");
                authContext.Factor = GetClaimValue(token, "auth_factor");
            }

            return new IdToken
            {
                Username = preferredUsername,
                AuthContext = authContext,
                AuthTime = GetClaimValueAsInt(token, "auth_time")
            };
        }

        private static string GetClaimValue(JsonWebToken token, string claimType)
        {
            if (token.TryGetPayloadValue<string>(claimType, out string value))
            {
                return value;
            }
            return null;
        }

        private static int GetClaimValueAsInt(JsonWebToken token, string claimType)
        {
            if (token.TryGetPayloadValue<int>(claimType, out int value))
            {
                return value;
            }
            return 0;
        }
    }
}
