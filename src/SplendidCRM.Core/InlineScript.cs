using System;
using System.IO;
using System.Text;

namespace SplendidCRM
{
	// 02/05/2008 Paul.  An AJAX UpdatePanel does not handle inline scripts.  The workaround is to use a user control 
	// to wrap the script so that it can be registered with the ScriptManager. 
	// http://weblogs.asp.net/infinitiesloop/archive/2007/09/17/inline-script-inside-an-asp-net-ajax-updatepanel.aspx
	//
	// Migration Note: In .NET Framework 4.8, InlineScript inherited from System.Web.UI.Control and used
	// ScriptManager to register inline scripts during AJAX UpdatePanel async postbacks. In ASP.NET Core,
	// UpdatePanels and ScriptManager do not exist. This class is preserved as a migration adapter stub
	// to maintain compilation compatibility with code that references InlineScript.
	// WebForms base class inheritance (Control) has been removed.
	// System.Web.UI.*, System.Web.UI.WebControls, System.Web.UI.HtmlControls usings removed.
	// System.Web using removed.
	public class InlineScript
	{
		/// <summary>
		/// When true, scripts were registered as client script blocks via ScriptManager.RegisterClientScriptBlock
		/// in the original WebForms implementation. When false, scripts were registered as startup scripts
		/// via ScriptManager.RegisterStartupScript. In ASP.NET Core, this property is preserved for
		/// API compatibility but has no behavioral effect since ScriptManager does not exist.
		/// </summary>
		public bool PageClientScript { get; set; }

		/// <summary>
		/// Renders inline script content to the provided TextWriter.
		/// In the original WebForms implementation, this method overrode Control.Render(HtmlTextWriter)
		/// to detect async postbacks via ScriptManager.GetCurrent(Page).IsInAsyncPostBack and register
		/// captured script content with ScriptManager. In non-async scenarios, it delegated to base.Render.
		/// In ASP.NET Core, this stub preserves the rendering pipeline structure for migration compatibility.
		/// </summary>
		/// <param name="writer">The TextWriter to render output to.</param>
		public void Render(TextWriter writer)
		{
			if ( writer == null )
			{
				return;
			}
			// In ASP.NET Core, there is no ScriptManager or UpdatePanel async postback mechanism.
			// The original WebForms code path was:
			//   1. Check ScriptManager.GetCurrent(Page).IsInAsyncPostBack
			//   2. If async postback: capture child control output into StringBuilder via StringWriter,
			//      then register the script with ScriptManager (RegisterClientScriptBlock or RegisterStartupScript
			//      depending on PageClientScript flag).
			//   3. If not async postback: delegate to base.Render(writer) for normal output.
			// This stub preserves the rendering pipeline structure using StringBuilder and StringWriter
			// for code that may call Render expecting the same method signature.
			StringBuilder sb = new StringBuilder();
			using ( StringWriter sw = new StringWriter(sb) )
			{
				// No child controls to render in ASP.NET Core — the WebForms Control.Render
				// pipeline is not available. Content would be written to sw if child controls existed.
				string script = sb.ToString();
				if ( !String.IsNullOrEmpty(script) )
				{
					// 03/21/2016 Paul.  False so that it matches startup implementation.
					// In WebForms, PageClientScript determined whether RegisterClientScriptBlock
					// or RegisterStartupScript was called. Here we simply write the content.
					writer.Write(script);
				}
			}
		}
	}
}
