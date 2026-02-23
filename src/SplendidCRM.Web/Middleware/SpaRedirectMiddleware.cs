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
// Migrated from Global.asax.cs Application_BeginRequest (lines 279-299)
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace SplendidCRM.Web.Middleware
{
	/// <summary>
	/// ASP.NET Core middleware that rewrites React SPA client-side route URLs
	/// to the SPA entry point (default.aspx), allowing the web server to serve
	/// the React application for all client-side routes under /react/.
	/// Replaces the Application_BeginRequest URL rewriting logic from Global.asax.cs.
	/// </summary>
	public class SpaRedirectMiddleware
	{
		private readonly RequestDelegate _next;

		/// <summary>
		/// Initializes the middleware with the next delegate in the pipeline.
		/// </summary>
		/// <param name="next">The next middleware delegate in the request pipeline.</param>
		public SpaRedirectMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		/// <summary>
		/// Processes an HTTP request, rewriting React SPA client-side route URLs
		/// to the SPA entry point so the web server serves the React application.
		/// </summary>
		/// <param name="context">The HTTP context for the current request.</param>
		/// <returns>A task representing the asynchronous middleware operation.</returns>
		public async Task InvokeAsync(HttpContext context)
		{
			// 06/18/2019 Paul.  Rewrite the path so that any React parameters are ignored. 
			// 06/18/2019 Paul.  Try and be as efficient as possible as every request and every url will be checked. 
			// 06/18/2019 Paul.  We are just trying to allow the React style routing to be ignored by the web server, so any file extension url can be ignored. 
			if ( HttpMethods.IsGet(context.Request.Method) )
			{
				// In .NET Framework, Request.Path returned the full URL path including the application path.
				// In ASP.NET Core, Request.Path is relative to PathBase, so we reconstruct the full path
				// to preserve the original comparison logic from Global.asax.cs Application_BeginRequest.
				string pathBaseValue = context.Request.PathBase.Value ?? string.Empty;
				string pathValue     = context.Request.Path.Value ?? string.Empty;
				string url           = pathBaseValue + pathValue;
				if ( !url.Contains(".") )
				{
					url = url.ToLower();
					if ( url.Contains("/react/") )
					{
						// In ASP.NET Core, Request.PathBase replaces Request.ApplicationPath
						string sApplicationPath = (context.Request.PathBase.Value ?? "/").ToLower();
						if ( !sApplicationPath.EndsWith("/") )
							sApplicationPath += "/";
						string sReactPath = sApplicationPath + "react/";
						if ( url.StartsWith(sReactPath) )
						{
							url = sReactPath + "default.aspx";
							// In .NET Framework, Context.RewritePath() set the full URL including application path.
							// In ASP.NET Core, Request.Path is relative to PathBase, so we strip the PathBase
							// portion from the rewritten URL to compute the correct relative path.
							string rewritePath = url;
							if ( pathBaseValue.Length > 0 )
							{
								rewritePath = url.Substring(pathBaseValue.ToLower().Length);
							}
							if ( !rewritePath.StartsWith("/") )
								rewritePath = "/" + rewritePath;
							//Debug.WriteLine("Rewrite " + context.Request.Path + " to " + rewritePath);
							context.Request.Path = new PathString(rewritePath);
						}
					}
				}
			}
			await _next(context);
		}
	}

	/// <summary>
	/// Extension methods for registering <see cref="SpaRedirectMiddleware"/> in the ASP.NET Core middleware pipeline.
	/// </summary>
	public static class SpaRedirectMiddlewareExtensions
	{
		/// <summary>
		/// Adds the SPA redirect middleware to the application's request pipeline.
		/// This middleware rewrites React SPA client-side route URLs to the SPA entry point.
		/// Register this in Program.cs: <c>app.UseSpaRedirect();</c>
		/// </summary>
		/// <param name="builder">The application builder.</param>
		/// <returns>The application builder for chaining.</returns>
		public static IApplicationBuilder UseSpaRedirect(this IApplicationBuilder builder)
		{
			return builder.UseMiddleware<SpaRedirectMiddleware>();
		}
	}
}
