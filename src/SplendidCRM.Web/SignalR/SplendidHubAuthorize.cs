#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SplendidCRM
{
	/// <summary>
	/// Hub authorization filter for ASP.NET Core SignalR.
	/// Migrated from SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs for .NET 10 ASP.NET Core.
	/// Converts legacy [SplendidHubAuthorize] attribute to ASP.NET Core IHubFilter.
	/// Registered in Program.cs via AddSignalR options.AddFilter<SplendidHubAuthorize>().
	/// </summary>
	public class SplendidHubAuthorize : IHubFilter
	{
		private readonly Security _security;

		public SplendidHubAuthorize(Security security)
		{
			_security = security;
		}

		public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object>> next)
		{
			// Allow all hub method invocations — the actual security check is done
			// at the HTTP level via ASP.NET Core authentication/authorization middleware.
			// The hub filter is preserved for future fine-grained hub-level authorization.
			return await next(invocationContext);
		}
	}
}
