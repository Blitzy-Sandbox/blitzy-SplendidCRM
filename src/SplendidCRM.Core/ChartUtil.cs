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
// .NET 10 Migration: SplendidCRM/_code/ChartUtil.cs → src/SplendidCRM.Core/ChartUtil.cs
// Changes applied:
//   - REMOVED: using System.Web; using System.Web.UI; using System.Web.UI.WebControls;
//              using System.Web.UI.HtmlControls; using System.Web.Optimization; (all WebForms namespaces)
//   - ADDED:   using Microsoft.Extensions.Caching.Memory; (IMemoryCache replacing Application[])
//   - REMOVED: static method parameter RegisterScripts(Page Page) — Page is a System.Web.UI type
//              not available in .NET 10 ASP.NET Core. Method is now an instance method with no parameters.
//   - REMOVED: AjaxControlToolkit.ToolkitScriptManager — WebForms AjaxControlToolkit not available in .NET 10
//   - REMOVED: Bundle, BundleTable (System.Web.Optimization) — jqPlot chart script bundling is now
//              handled by the React frontend build pipeline (see SplendidCRM/React/)
//   - REMOVED: Sql.AddScriptReference(), Sql.AddStyleSheet() — WebForms-only methods that were removed
//              from the migrated Sql.cs as part of the .NET 10 migration
//   - REPLACED: HttpContext.Current.Application["SplendidVersion"]
//              → _memoryCache.Get<object>("SplendidVersion") via IMemoryCache DI
//   - ADDED:   DI constructor accepting IMemoryCache to replace HttpApplicationState (Application[]) access
//   - PRESERVED: sBundleName construction logic (versioned bundle name identifier retained as reference)
//   - PRESERVED: Error handling pattern with SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex)
#nullable disable
using System;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace SplendidCRM
{
	/// <summary>
	/// Chart rendering utility for SplendidCRM.
	/// Migrated from SplendidCRM/_code/ChartUtil.cs for .NET 10 ASP.NET Core.
	///
	/// The original WebForms implementation registered jqPlot JavaScript bundles via
	/// System.Web.Optimization.Bundle and AjaxControlToolkit.ToolkitScriptManager.
	/// These types are exclusive to the .NET Framework WebForms pipeline and are not
	/// available in .NET 10 ASP.NET Core.
	///
	/// In the .NET 10 architecture, jqPlot chart scripts are served as static assets
	/// managed by the React frontend build pipeline (SplendidCRM/React/). The bundle
	/// name construction logic is preserved in RegisterScripts() to retain the versioned
	/// cache-busting naming convention for potential future use by the frontend.
	/// </summary>
	public class ChartUtil
	{
		// =====================================================================================
		// .NET 10 Migration: IMemoryCache replaces HttpApplicationState (Application[])
		// BEFORE: HttpContext.Current.Application["SplendidVersion"]
		// AFTER:  _memoryCache.Get<object>("SplendidVersion")
		// =====================================================================================
		private readonly IMemoryCache _memoryCache;

		/// <summary>
		/// Constructs a ChartUtil instance with an injected memory cache.
		/// </summary>
		/// <param name="memoryCache">
		/// Replaces HttpApplicationState (Application[]) for reading the cached SplendidVersion
		/// value used in versioned chart bundle naming. Register as a singleton or scoped service.
		/// </param>
		public ChartUtil(IMemoryCache memoryCache)
		{
			_memoryCache = memoryCache;
		}

		/// <summary>
		/// Registers chart scripts for the current request context.
		///
		/// .NET 10 Migration Notes:
		/// The original WebForms implementation (SplendidCRM/_code/ChartUtil.cs) registered
		/// jqPlot JavaScript and its 30 plugin files into a System.Web.Optimization.Bundle,
		/// added it to BundleTable.Bundles, and registered the bundle reference via
		/// AjaxControlToolkit.ToolkitScriptManager. None of these APIs exist in .NET 10 ASP.NET Core.
		///
		/// In the migrated architecture, jqPlot chart scripts are served as static files managed
		/// by the React frontend (SplendidCRM/React/). This method preserves the versioned
		/// bundle name construction — using the cached SplendidVersion for cache-busting —
		/// as a reference identifier. All WebForms bundle and script manager operations
		/// are intentionally removed.
		///
		/// Original source: SplendidCRM/_code/ChartUtil.cs → RegisterScripts(Page Page)
		/// </summary>
		public void RegisterScripts()
		{
			try
			{
				// 07/01/2017 Paul.  Use Microsoft ASP.NET Web Optimization 1.1.3 to combine stylesheets and javascript.
				// 01/24/2018 Paul.  Include version in url to ensure updates of combined files.
				// .NET 10 Migration: Application["SplendidVersion"] → _memoryCache.Get<object>("SplendidVersion")
				string sBundleName = "~/Charts/ChartScriptsCombined" + "_" + Sql.ToString(_memoryCache.Get<object>("SplendidVersion"));

				// .NET 10 Migration: The following WebForms bundle and script registration operations
				// are removed because Bundle (System.Web.Optimization), BundleTable, and
				// AjaxControlToolkit.ToolkitScriptManager are not available in .NET 10 ASP.NET Core.
				//
				// jqPlot and its plugin scripts are now served as static files by the React frontend.
				// The sBundleName identifier above is retained for reference continuity and may be
				// consumed by the frontend asset pipeline when constructing versioned URLs.
				//
				// Original WebForms operations removed:
				//   AjaxControlToolkit.ToolkitScriptManager mgrAjax = ScriptManager.GetCurrent(Page) as AjaxControlToolkit.ToolkitScriptManager;
				//   Bundle bndChartScripts = new Bundle(sBundleName);
				//   bndChartScripts.Include("~/Include/jqPlot/jquery.jqplot.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.barRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.BezierCurveRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.blockRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.bubbleRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.canvasAxisLabelRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.canvasAxisTickRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.canvasOverlay.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.canvasTextRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.categoryAxisRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.ciParser.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.cursor.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.dateAxisRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.donutRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.dragable.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.enhancedLegendRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.funnelRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.highlighter.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.json2.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.logAxisRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.mekkoAxisRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.mekkoRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.meterGaugeRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.ohlcRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.pieRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.pointLabels.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.pyramidAxisRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.pyramidGridRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.pyramidRenderer.min.js");
				//   bndChartScripts.Include("~/Include/jqPlot/plugins/jqplot.trendline.min.js");
				//   BundleTable.Bundles.Add(bndChartScripts);
				//   Sql.AddScriptReference(mgrAjax, sBundleName);   // WebForms-only; removed from migrated Sql.cs
				//   Sql.AddStyleSheet(Page, "~/Include/jqPlot/jquery.jqplot.min.css");  // WebForms-only; removed from migrated Sql.cs
			}
			catch(Exception ex)
			{
				SplendidError.SystemError(new StackTrace(true).GetFrame(0), ex);
			}
		}
	}
}
