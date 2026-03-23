#region License

/*
 * Copyright 2011-2012 the original author or authors.
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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/OpenGraphTemplate.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Http;       — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest DLL has no .NET 10 NuGet equivalent
//   - KEPT: All System.* using directives, class/method signatures, business logic, #region blocks
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP §0.7.4 (Spring.Social Dependency Removal) and §0.8.1 (Minimal Change Clause).

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Spring.Social.Facebook.Api.Impl
{
	/// <summary>
	/// Implementation of <see cref="IOpenGraphOperations"/> that publishes namespace-qualified
	/// Open Graph actions through the Facebook Graph API.
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	public class OpenGraphTemplate : AbstractFacebookOperations, IOpenGraphOperations
	{
		public OpenGraphTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region IOpenGraphOperations Members
		/// <summary>
		/// Posts an action for an object specified by the given object URL.
		/// </summary>
		/// <param name="action">The application-specific action to post, without the application's namespace. (eg, "drink")</param>
		/// <param name="objectType">The application-specific object type, without the application's namespace. (eg, "beverage")</param>
		/// <param name="objectUrl">The URL of the object that is the target of the action.</param>
		/// <returns>The ID of the posted action.</returns>
		public string PublishAction(string action, string objectType, string objectUrl)
		{
			requireAuthorization();
			NameValueCollection parameters = new NameValueCollection();
			parameters.Set(objectType, objectUrl);
			return this.Publish("me", this.applicationNamespace + ":" + action, parameters);
		}

		private void requireApplicationNamespace()
		{
			if ( applicationNamespace == null || String.IsNullOrEmpty(applicationNamespace) )
			{
				throw new Exception("MissingNamespaceException");  // MissingNamespaceException();
			}
		}
		#endregion
	}
}
