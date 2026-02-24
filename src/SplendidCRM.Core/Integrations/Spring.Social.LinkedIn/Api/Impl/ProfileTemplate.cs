#region License

/*
 * Copyright 2002-2012 the original author or authors.
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

// MIGRATION: Migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core.
// Removed conditional #if NET_4_0 || SILVERLIGHT_5 / #else blocks.
// Kept ONLY the Task-based async code path (previously under #if NET_4_0 || SILVERLIGHT_5).
// Removed the #else branch containing:
//   - Synchronous methods (GetUserProfile, GetUserProfileById, GetUserProfileByPublicUrl,
//     GetUserFullProfile, GetUserFullProfileById, GetUserFullProfileByPublicUrl, Search)
//     guarded by #if !SILVERLIGHT
//   - Callback-based async methods using Spring.Rest.Client.RestOperationCanceler
//   - Nested #if !WINDOWS_PHONE guards for public URL callback overloads
// System.Threading.Tasks import is now unconditional (was previously conditional under
// #if NET_4_0 || SILVERLIGHT_5).
// System.Collections.Specialized import is now unconditional (was previously guarded by
// #if SILVERLIGHT / #else — the desktop path #else branch is kept, Silverlight path removed).
// Spring.Collections.Specialized import (Silverlight path) removed — desktop path only.
// Spring.Rest.Client.RestTemplate field and constructor preserved for structural fidelity.
// Method bodies stub via Task.FromResult — Spring.Rest.Client.RestTemplate.GetForObjectAsync
// is not available on .NET 10; stubs ensure dormant Enterprise Edition integration compiles.
// This is a dormant Enterprise Edition stub — compile only, not activated.

#nullable disable
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Spring.Http;
using Spring.Rest.Client;

namespace Spring.Social.LinkedIn.Api.Impl
{
    /// <summary>
    /// Implementation of <see cref="IProfileOperations"/>, providing a binding to LinkedIn's profiles-oriented REST resources.
    /// </summary>
    /// <author>Bruno Baia</author>
    class ProfileTemplate : AbstractLinkedInOperations, IProfileOperations
    {
        private const string ProfileUrl = "people/{id}:(id,first-name,last-name,headline,industry,public-profile-url,picture-url,summary,site-standard-profile-request,api-standard-profile-request)?format=json";
        private const string FullProfileUrl = "people/{id}:(id,first-name,last-name,headline,industry,public-profile-url,picture-url,summary,site-standard-profile-request,api-standard-profile-request,location,distance,num-connections,num-connections-capped,specialties,proposal-comments,associations,honors,interests,positions,skills,educations,num-recommenders,recommendations-received,phone-numbers,im-accounts,twitter-accounts,date-of-birth,main-address,member-url-resources)?format=json";
        private const string SearchUrl = "https://api.linkedin.com/v1/people-search:(people:(id,first-name,last-name,headline,industry,public-profile-url,picture-url,summary,site-standard-profile-request,api-standard-profile-request))?format=json";

        private RestTemplate restTemplate;

        public ProfileTemplate(RestTemplate restTemplate)
        {
            this.restTemplate = restTemplate;
        }

        #region IProfileOperations Members

        /// <summary>
        /// Asynchronously retrieves the authenticated user's LinkedIn profile details.
        /// </summary>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// a <see cref="LinkedInProfile"/> object representing the user's profile.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInProfile> GetUserProfileAsync()
        {
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInProfile>(ProfileUrl, "~");
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            return Task.FromResult(new LinkedInProfile());
        }

        /// <summary>
        /// Asynchronously retrieves a specific user's LinkedIn profile details by its ID.
        /// </summary>
        /// <param name="id">The user ID for the user whose details are to be retrieved.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// a <see cref="LinkedInProfile"/> object representing the user's profile.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInProfile> GetUserProfileByIdAsync(string id)
        {
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInProfile>(ProfileUrl, "id=" + id);
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            return Task.FromResult(new LinkedInProfile());
        }

        /// <summary>
        /// Asynchronously retrieves a specific user's LinkedIn profile details by its public url.
        /// </summary>
        /// <param name="url">The user public url for the user whose details are to be retrieved.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// a <see cref="LinkedInProfile"/> object representing the user's profile.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInProfile> GetUserProfileByPublicUrlAsync(string url)
        {
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInProfile>(ProfileUrl, "url=" + HttpUtils.UrlEncode(url));
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            _ = url; // suppress unused parameter warning; preserved for Enterprise Edition activation
            return Task.FromResult(new LinkedInProfile());
        }

        /// <summary>
        /// Asynchronously retrieves the authenticated user's LinkedIn full profile details.
        /// </summary>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// a <see cref="LinkedInFullProfile"/> object representing the full user's profile.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInFullProfile> GetUserFullProfileAsync()
        {
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInFullProfile>(FullProfileUrl, "~");
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            return Task.FromResult(new LinkedInFullProfile());
        }

        /// <summary>
        /// Asynchronously retrieves a specific user's LinkedIn full profile details by its ID.
        /// </summary>
        /// <param name="id">The user ID for the user whose details are to be retrieved.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// a <see cref="LinkedInFullProfile"/> object representing the full user's profile.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInFullProfile> GetUserFullProfileByIdAsync(string id)
        {
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInFullProfile>(FullProfileUrl, "id=" + id);
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            return Task.FromResult(new LinkedInFullProfile());
        }

        /// <summary>
        /// Asynchronously retrieves a specific user's LinkedIn full profile details by its public url.
        /// </summary>
        /// <param name="url">The user public url for the user whose details are to be retrieved.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// a <see cref="LinkedInFullProfile"/> object representing the full user's profile.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInFullProfile> GetUserFullProfileByPublicUrlAsync(string url)
        {
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInFullProfile>(FullProfileUrl, "url=" + HttpUtils.UrlEncode(url));
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            _ = url; // suppress unused parameter warning; preserved for Enterprise Edition activation
            return Task.FromResult(new LinkedInFullProfile());
        }

        /// <summary>
        /// Asynchronously searches for LinkedIn profiles based on provided parameters.
        /// </summary>
        /// <param name="searchParams">The profile search parameters.</param>
        /// <returns>
        /// A <code>Task</code> that represents the asynchronous operation that can return 
        /// an object containing the search results metadata and a list of matching <see cref="LinkedInProfile"/>s.
        /// </returns>
        /// <exception cref="LinkedInApiException">If there is an error while communicating with LinkedIn.</exception>
        public Task<LinkedInProfiles> SearchAsync(SearchParameters searchParams)
        {
            NameValueCollection parameters = BuildSearchParameters(searchParams);
            string url = this.BuildUrl(SearchUrl, parameters);
            // MIGRATION STUB: Spring.Rest.Client.RestTemplate.GetForObjectAsync not available on .NET 10.
            // Original: return this.restTemplate.GetForObjectAsync<LinkedInProfiles>(this.BuildUrl(SearchUrl, parameters));
            // Returns empty result — dormant Enterprise Edition stub, not activated.
            _ = url; // suppress unused variable warning; url preserved for Enterprise Edition activation
            return Task.FromResult(new LinkedInProfiles());
        }

        #endregion

        #region Private Methods

        private static NameValueCollection BuildSearchParameters(SearchParameters searchParams)
        {
            NameValueCollection parameters = new NameValueCollection();
            if (!String.IsNullOrEmpty(searchParams.Keywords))
            {
                parameters.Add("keywords", searchParams.Keywords);
            }
            if (!String.IsNullOrEmpty(searchParams.FirstName))
            {
                parameters.Add("first-name", searchParams.FirstName);
            }
            if (!String.IsNullOrEmpty(searchParams.LastName))
            {
                parameters.Add("last-name", searchParams.LastName);
            }
            if (!String.IsNullOrEmpty(searchParams.CompanyName))
            {
                parameters.Add("company-name", searchParams.CompanyName);
            }
            if (searchParams.IsCurrentCompany.HasValue)
            {
                parameters.Add("current-company", searchParams.IsCurrentCompany.Value.ToString().ToLowerInvariant());
            }
            if (!String.IsNullOrEmpty(searchParams.Title))
            {
                parameters.Add("title", searchParams.Title);
            }
            if (searchParams.IsCurrentTitle.HasValue)
            {
                parameters.Add("current-title", searchParams.IsCurrentTitle.Value.ToString().ToLowerInvariant());
            }
            if (!String.IsNullOrEmpty(searchParams.SchoolName))
            {
                parameters.Add("school-name", searchParams.SchoolName);
            }
            if (searchParams.IsCurrentSchool.HasValue)
            {
                parameters.Add("current-school", searchParams.IsCurrentSchool.Value.ToString().ToLowerInvariant());
            }
            if (!String.IsNullOrEmpty(searchParams.CountryCode))
            {
                parameters.Add("country-code", searchParams.CountryCode.ToLowerInvariant());
            }
            if (!String.IsNullOrEmpty(searchParams.PostalCode))
            {
                parameters.Add("postal-code", searchParams.PostalCode);
            }
            if (searchParams.Distance.HasValue)
            {
                parameters.Add("distance", searchParams.Distance.Value.ToString());
            }
            if (searchParams.Start.HasValue)
            {
                parameters.Add("start", searchParams.Start.Value.ToString());
            }
            if (searchParams.Count.HasValue)
            {
                parameters.Add("count", searchParams.Count.Value.ToString());
            }
            if (searchParams.Sort.HasValue)
            {
                parameters.Add("sort", searchParams.Sort.Value.ToString().ToLowerInvariant());
            }
            return parameters;
        }

        #endregion
    }
}
