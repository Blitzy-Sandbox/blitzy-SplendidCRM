#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Salesforce.Api;

namespace Spring.Social.Salesforce.Api.Impl
{
    // .NET 10 Migration: Spring.Rest.Client and Spring.Http dependencies removed.
    // Method bodies use dormant stub returns — this integration is not activated at runtime.
    class VersionTemplate : AbstractSalesforceOperations, IVersionOperations
    {
        /// <summary>
        /// Lists summary information about each Salesforce version currently available,
        /// including the version, label, and a link to each version's root.
        /// </summary>
        /// <returns>A list of Version objects.</returns>
        public List<SalesforceVersion> GetVersions() { return new List<SalesforceVersion>(); }

        /// <summary>
        /// Lists available resources for the specified API version, including resource name and URI.
        /// </summary>
        /// <param name="version">Version number.</param>
        /// <returns>Resources object.</returns>
        public SalesforceResources GetResourcesByVersion(string version) { return null; }
    }
}
