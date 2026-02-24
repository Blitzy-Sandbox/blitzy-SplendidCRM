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

// .NET 10 Migration: Removed discontinued Spring.Social.OAuth2 using directive.
// Spring.Social.OAuth2 (Spring.Social.Core.dll) has no .NET Core / .NET 10 NuGet equivalent
// per AAP Section 0.7.4. Local stub classes OAuth2Template and AbstractOAuth2ServiceProvider<T>
// are defined below to satisfy compile-time requirements while preserving the public API surface
// for the Enterprise Edition upgrade path. This is a dormant integration stub — must compile
// but is NOT expected to execute.
using Spring.Social.HubSpot.Api;

namespace Spring.Social.HubSpot.Connect
{
	// Stub class replacing Spring.Social.OAuth2.OAuth2Template (no .NET 10 equivalent).
	// Dormant stub — preserves constructor signature for Enterprise Edition upgrade path.
	// Per AAP Section 0.7.4: Spring.Social.OAuth2 is discontinued; minimal stub defined locally.
	public class OAuth2Template
	{
		public OAuth2Template(string clientId, string clientSecret, string authorizeUrl, string accessTokenUrl)
		{
		}
	}

	// Stub class replacing Spring.Social.OAuth2.AbstractOAuth2ServiceProvider<T> (no .NET 10 equivalent).
	// Dormant stub — preserves inheritance contract for Enterprise Edition upgrade path.
	// Per AAP Section 0.7.4: Spring.Social.OAuth2 is discontinued; minimal stub defined locally.
	public abstract class AbstractOAuth2ServiceProvider<T> where T : class
	{
		protected AbstractOAuth2ServiceProvider(OAuth2Template oAuth2Template)
		{
		}

		public abstract T GetApi(string accessToken);
	}

	public class HubSpotServiceProvider : AbstractOAuth2ServiceProvider<IHubSpot>
	{
		public HubSpotServiceProvider(string clientId, string clientSecret)
			: base(new OAuth2Template(clientId, clientSecret, String.Empty, String.Empty))
		{
		}

		public override IHubSpot GetApi(String accessToken)
		{
			throw(new Exception("not implemented"));
		}
	}
}
