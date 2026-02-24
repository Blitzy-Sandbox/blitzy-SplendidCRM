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

// Migrated from SplendidCRM/_code/Spring.Social.Facebook/Api/Impl/FqlTemplate.cs
// Changes (.NET Framework 4.8 → .NET 10 ASP.NET Core migration):
//   - REMOVED: using Spring.Http;      — Spring.Http has no .NET 10 NuGet equivalent
//   - REMOVED: using Spring.Rest.Client; — Spring.Rest DLL has no .NET 10 NuGet equivalent
//   - ADDED: using Spring.Social.Facebook.Api; for IFqlOperations resolution
//   - KEPT: All class/method signatures, constructor, business logic, #region blocks,
//           XML doc comments, and Apache License 2.0 header preserved exactly.
//   - NOTE: RestTemplate stub and HttpUtils stub are defined in AbstractFacebookOperations.cs
//           (same Spring.Social.Facebook.Api.Impl namespace) — no additional import required.
// This is a dormant Enterprise Edition stub — compile-only, NOT activated at runtime.
// See AAP 0.7.4 (Spring.Social Dependency Removal) and AAP 0.8.1 (Minimal Change Clause).

using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;

using Spring.Social.Facebook.Api;

namespace Spring.Social.Facebook.Api.Impl
{
	/// <summary>
	/// </summary>
	/// <author>SplendidCRM (.NET)</author>
	class FqlTemplate : AbstractFacebookOperations, IFqlOperations
	{
		public FqlTemplate(string applicationNamespace, RestTemplate restTemplate, bool isAuthorized)
			: base(applicationNamespace, restTemplate, isAuthorized)
		{
		}

		#region IFqlOperations Members
		public T QueryFQL<T>(string fql) where T : class
		{
			// http://developers.facebook.com/docs/reference/fql/
			return restTemplate.GetForObject<T>("fql?q=" + HttpUtils.FormEncode(fql));
		}
		#endregion
	}
}
