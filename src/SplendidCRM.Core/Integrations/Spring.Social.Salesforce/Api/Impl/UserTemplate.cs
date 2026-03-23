#nullable disable
#region License

/*
 * Copyright (C) 2012 SplendidCRM Software, Inc. All Rights Reserved. 
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

// .NET 10 Migration: Removed Spring.Json, Spring.Http, and Spring.Rest.Client using directives.
// These Spring.NET Framework assemblies are discontinued with no .NET Core / .NET 10 equivalent.
// RestTemplate and JsonValue stub types are defined in AbstractSalesforceOperations.cs within
// the same Spring.Social.Salesforce.Api.Impl namespace and are resolved without explicit using
// directives. This is a dormant integration stub — compiles on .NET 10 but NOT executed at runtime.

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Salesforce.Api.Impl
{
	/// <summary>
	/// Implements Salesforce user password management operations via the Salesforce REST API.
	/// Supports getting password expiration status, setting a new password, and resetting
	/// the password to a system-generated value.
	/// </summary>
	/// <remarks>
	/// This is a dormant Enterprise Edition stub that compiles on .NET 10 but is NOT activated
	/// or executed at runtime. All Spring.NET Framework dependencies have been replaced with
	/// stub types defined in <see cref="AbstractSalesforceOperations"/> (AAP section 0.7.4).
	/// </remarks>
	/// <author>SplendidCRM (.NET)</author>
	class UserTemplate : AbstractSalesforceOperations, IUserOperations
	{
		/// <summary>
		/// Creates a new instance of <see cref="UserTemplate"/>.
		/// </summary>
		/// <param name="restTemplate">The REST template stub used for HTTP requests.</param>
		/// <param name="isAuthorized">
		/// Whether the API binding was created with valid OAuth 2.0 authorization credentials.
		/// </param>
		public UserTemplate(RestTemplate restTemplate, bool isAuthorized) : base(restTemplate, isAuthorized)
		{
		}

		#region IUserOperations Members

		/// <summary>
		/// Gets the password expiration status for a Salesforce user.
		/// Performs a GET request to <c>/services/data/v{version}/sobjects/User/{userId}/password</c>
		/// and returns whether the user's password has expired.
		/// </summary>
		/// <param name="version">The Salesforce API version (e.g. "29.0").</param>
		/// <param name="userId">The Salesforce user ID.</param>
		/// <returns>
		/// <c>true</c> if the user's password has expired; <c>false</c> otherwise or if the
		/// response is null or does not contain the <c>isExpired</c> field.
		/// </returns>
		public bool GetPasswordExpiration(string version, string userId)
		{
			requireAuthorization();
			JsonValue json = this.restTemplate.GetForObject<JsonValue>("/services/data/v" + version + "/sobjects/User/" + userId + "/password");
			if ( json != null && !json.IsNull && json.ContainsName("isExpired") )
			{
				return json.GetValue<bool>("isExpired");
			}
			return false;
		}

		/// <summary>
		/// Sets the password for a Salesforce user to the specified value.
		/// Performs a POST request to <c>/services/data/v{version}/sobjects/User/{userId}/password</c>
		/// with a JSON body containing the new password.
		/// </summary>
		/// <param name="version">The Salesforce API version (e.g. "29.0").</param>
		/// <param name="userId">The Salesforce user ID.</param>
		/// <param name="password">The new password to set for the user.</param>
		public void SetPassword(string version, string userId, string password)
		{
			requireAuthorization();
			this.restTemplate.PostForObject<JsonValue>("/services/data/v" + version + "/sobjects/User/" + userId + "/password", "{\"NewPassword\", \"" + password + "\"}");
		}

		/// <summary>
		/// Resets the password for a Salesforce user to a system-generated value.
		/// Performs a DELETE request to <c>/services/data/v{version}/sobjects/User/{userId}/password</c>
		/// and returns the newly generated password from the <c>NewPassword</c> response field.
		/// </summary>
		/// <param name="version">The Salesforce API version (e.g. "29.0").</param>
		/// <param name="userId">The Salesforce user ID.</param>
		/// <returns>
		/// The new system-generated password string, or <see cref="String.Empty"/> if the
		/// response is null or does not contain the <c>NewPassword</c> field.
		/// </returns>
		public string ResetPassword(string version, string userId)
		{
			requireAuthorization();
			JsonValue json = Delete<JsonValue>("/services/data/v" + version + "/sobjects/User/" + userId + "/password");
			if ( json != null && !json.IsNull && json.ContainsName("NewPassword") )
			{
				return json.GetValue<string>("NewPassword");
			}
			return String.Empty;
		}

		#endregion
	}
}
