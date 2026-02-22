/*******************************************************************************************************************
 * SplendidCRM is a Customer Relationship Management program created by SplendidCRM Software, Inc.
 * Copyright (C) 2005-2023 SplendidCRM Software, Inc. All rights reserved.
 *
 * Any use of the contents of this file are subject to the SplendidCRM Community Source Code License
 * Agreement, or other written agreement between you and SplendidCRM ("License"). By installing or
 * using this file, you have unconditionally agreed to the terms and conditions of the License,
 * including but not limited to restrictions on the number of users therein, and you may not use this
 * file except in compliance with the License.
 *
 * SplendidCRM owns all proprietary rights, including all copyrights, patents, trade secrets, and
 * trademarks, in and to the contents of this file. You will not link to or in any way combine the
 * contents of this file or any derivatives with any Open Source Code in any manner that would require
 * the contents of this file to be made available to any third party.
 *
 * IN NO EVENT SHALL SPLENDIDCRM BE RESPONSIBLE FOR ANY DAMAGES OF ANY NATURE WHATSOEVER ARISING OUT
 * OF OR IN CONNECTION WITH THE USE OF OR INABILITY TO USE THIS FILE.
 *
 * IMPORTANT NOTE: This file was migrated from .NET Framework 4.8 to .NET 10 ASP.NET Core as part of
 * the SplendidCRM backend modernization (Prompt 1 of 3). The following cross-cutting transformations
 * were applied:
 *   - Replaced using System.Web → using Microsoft.AspNetCore.Http
 *   - Replaced static class pattern with instance-based DI (IHttpContextAccessor constructor injection)
 *   - Preserved namespace SplendidCRM and all public method signatures
 *   - Minimal change clause: only framework migration changes applied
 *********************************************************************************************************************/
using Microsoft.AspNetCore.Http;

namespace SplendidCRM
{
	/// <summary>
	/// Workflow processing utilities.
	/// Provides a Process method for executing pending workflow rules.
	/// Migrated from static class with System.Web.HttpContext to instance-based
	/// class with IHttpContextAccessor dependency injection.
	/// </summary>
	public class WorkflowUtils
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		/// <summary>
		/// Initializes a new instance of the <see cref="WorkflowUtils"/> class.
		/// </summary>
		/// <param name="httpContextAccessor">
		/// The HTTP context accessor for accessing the current request context,
		/// replacing the legacy static HttpContext.Current access pattern.
		/// </param>
		public WorkflowUtils(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor;
		}

		/// <summary>
		/// Process pending workflow rules.
		/// Called by SchedulerUtils.OnTimer after iterating through scheduled jobs
		/// to process any pending workflow rules that may have been triggered.
		/// </summary>
		/// <param name="Context">The current HTTP context for the request.</param>
		/// <remarks>
		/// This is a Community Edition stub. The workflow engine implementation
		/// is available in SplendidCRM Enterprise Edition.
		/// The method body is intentionally empty to match the original source.
		/// </remarks>
		public void Process(HttpContext Context)
		{
			// Community Edition stub — workflow processing is an Enterprise Edition feature.
			// The method signature is preserved for API contract compatibility with
			// SchedulerUtils.OnTimer which calls WorkflowUtils.Process(HttpContext).
		}
	}
}
