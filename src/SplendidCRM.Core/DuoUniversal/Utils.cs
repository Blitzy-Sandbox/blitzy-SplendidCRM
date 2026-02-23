// SPDX-FileCopyrightText: 2022 Cisco Systems, Inc. and/or its affiliates
//
// SPDX-License-Identifier: BSD-3-Clause
// Migrated from SplendidCRM/_code/DuoUniversal/Utils.cs for .NET 10

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace DuoUniversal
{
    /// <summary>
    /// Utility methods for the DuoUniversal client.
    /// </summary>
    internal static class Utils
    {
        /// <summary>
        /// Generate a cryptographically random string of the specified length.
        /// Used for generating state values and nonces for the OIDC flow.
        /// </summary>
        internal static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var result = new char[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[length];
                rng.GetBytes(bytes);
                for (int i = 0; i < length; i++)
                {
                    result[i] = chars[bytes[i] % chars.Length];
                }
            }
            return new string(result);
        }

        /// <summary>
        /// Encode a string as a Base64Url string (RFC 4648).
        /// </summary>
        internal static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Decode a Base64Url encoded string.
        /// </summary>
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
        /// Create a form-encoded content object from a dictionary of key-value pairs.
        /// </summary>
        internal static FormUrlEncodedContent CreateFormContent(IDictionary<string, string> parameters)
        {
            return new FormUrlEncodedContent(parameters);
        }

        /// <summary>
        /// Create an HTTP Basic Authorization header value from client ID and secret.
        /// </summary>
        internal static string CreateBasicAuthHeader(string clientId, string clientSecret)
        {
            string credentials = $"{clientId}:{clientSecret}";
            byte[] bytes = Encoding.UTF8.GetBytes(credentials);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Validate that a string is not null, empty, or whitespace.
        /// </summary>
        internal static void ValidateRequiredStringParam(string paramName, string paramValue)
        {
            if (string.IsNullOrWhiteSpace(paramValue))
            {
                throw new DuoException($"'{paramName}' must not be null or empty.");
            }
        }
    }
}
