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
// Removed: using Spring.Social.OAuth1; — Spring.Social.OAuth1 is discontinued with no .NET 10 NuGet equivalent.
// OAuth1Template and AbstractOAuth1ServiceProvider<T> are defined as local stubs below.
using Spring.Social.QuickBooks.Api;

namespace Spring.Social.QuickBooks.Connect
{
	// Stub class replacing Spring.Social.OAuth1.OAuth1Template (no .NET 10 equivalent).
	// Dormant stub — preserves constructor signature for Enterprise Edition upgrade path.
	// Migration note: Spring.Social.OAuth1 has no .NET Core/.NET 10 NuGet package;
	// the minimal OAuth1Template constructor contract is reproduced here as a no-op stub.
	public class OAuth1Template
	{
		public OAuth1Template(string consumerKey, string consumerSecret,
			string requestTokenUrl, string authorizeUrl, string authenticateUrl, string accessTokenUrl)
		{
		}
	}

	// Stub class replacing Spring.Social.OAuth1.AbstractOAuth1ServiceProvider<T> (no .NET 10 equivalent).
	// Dormant stub — preserves inheritance contract for Enterprise Edition upgrade path.
	// Migration note: Spring.Social.OAuth1 has no .NET Core/.NET 10 NuGet package;
	// the minimal AbstractOAuth1ServiceProvider<T> contract is reproduced here as a no-op stub.
	public abstract class AbstractOAuth1ServiceProvider<T> where T : class
	{
		protected AbstractOAuth1ServiceProvider(string consumerKey, string consumerSecret, OAuth1Template oAuth1Template)
		{
		}

		public abstract T GetApi(string accessToken, string secret);
	}

	public class QuickBooksServiceProvider : AbstractOAuth1ServiceProvider<IQuickBooks>
	{
		public QuickBooksServiceProvider(string consumerKey, string consumerSecret)
			: base(consumerKey, consumerSecret, new OAuth1Template(consumerKey, consumerSecret,
				"https://oauth.intuit.com/oauth/v1/get_request_token", 
				"https://workplace.intuit.com/Connect/Begin", 
				"https://appcenter.intuit.com/api/v1/authenticate", 
				"https://oauth.intuit.com/oauth/v1/get_access_token"))
		{
		}

		public override IQuickBooks GetApi(string accessToken, string secret)
		{
			throw(new Exception("GetApi requires a CompanyId"));
		}

		public IQuickBooks GetApi(string accessToken, string secret, string companyId)
		{
			throw(new Exception("Not implemented"));
		}
	}
}
