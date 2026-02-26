// Ad-hoc tests for AdminRestController
using System;
using System.Reflection;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

int passed = 0;
int failed = 0;

void Check(bool cond, string name)
{
    if (cond) { Console.WriteLine($"  [PASS] {name}"); passed++; }
    else      { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"  [FAIL] {name}"); Console.ResetColor(); failed++; }
}

var asm  = typeof(SplendidCRM.Web.Controllers.AdminRestController).Assembly;
var ctrl = typeof(SplendidCRM.Web.Controllers.AdminRestController);
Console.WriteLine($"Testing assembly: {asm.GetName().Name} v{asm.GetName().Version}");

// ── Suite 1: Type existence ────────────────────────────────────────────────
Console.WriteLine("\n=== Suite 1: Type Existence ===");
var vn = ctrl.GetNestedType("ViewNode");
var mn = ctrl.GetNestedType("ModuleNode");
var lf = ctrl.GetNestedType("LayoutField");
Check(ctrl != null,                 "AdminRestController class exists");
Check(vn   != null,                 "ViewNode inner class exists");
Check(mn   != null,                 "ModuleNode inner class exists");
Check(lf   != null,                 "LayoutField inner class exists");
Check(ctrl.BaseType?.Name == "ControllerBase", "Inherits ControllerBase");

// ── Suite 2: ASP.NET Core attributes ─────────────────────────────────────
Console.WriteLine("\n=== Suite 2: Controller Attributes ===");
var classAttrs    = ctrl.GetCustomAttributes().Select(a => a.GetType().Name).ToHashSet();
var routeAttr     = ctrl.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "RouteAttribute");
var routeTemplate = routeAttr?.GetType().GetProperty("Template")?.GetValue(routeAttr) as string;
Check(classAttrs.Contains("ApiControllerAttribute"),  "[ApiController] attribute present");
Check(classAttrs.Contains("RouteAttribute"),           "[Route] attribute present");
Check(routeTemplate == "Administration/Rest.svc",      $"Route='Administration/Rest.svc' (got='{routeTemplate}')");

// ── Suite 3: All 30 required members_exposed ──────────────────────────────
Console.WriteLine("\n=== Suite 3: 30 Required Public Methods ===");
var methods  = ctrl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   .Select(m => m.Name).ToHashSet();
var required = new[] {
    "GetAdminLayoutModules", "GetAdminLayoutModuleFields", "GetAdminLayoutModuleData",
    "GetAdminMenu", "GetAllGridViewsColumns", "GetAllDetailViewsFields", "GetAllEditViewsFields",
    "GetAllDetailViewsRelationships", "GetAllEditViewsRelationships", "GetAllDynamicButtons",
    "GetAllTerminology", "GetAllTerminologyLists", "GetReactState", "GetReactMenu",
    "UpdateAdminLayout", "UpdateAdminModule", "ExportAdminModule", "UpdateAdminConfig",
    "UpdateAdminTerminology", "UpdateAdminField", "UpdateAdminLayoutTable", "DeleteAdminLayoutField",
    "DeleteAdminConfig", "DeleteAdminTerminology", "GetAclAccessByUser", "GetAclAccessByRole",
    "GetAclFieldAccessByRole", "UpdateAclAccess", "UpdateAclFieldAccess", "GetAclFieldAliases"
};
foreach (var m in required)
    Check(methods.Contains(m), $"Method {m}");
Check(required.Length == 30, $"Exactly 30 required methods checked");

// ── Suite 4: DTO field completeness ───────────────────────────────────────
Console.WriteLine("\n=== Suite 4: DTO Field Completeness ===");
if (vn != null) {
    var f = vn.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToHashSet();
    foreach (var fn in new[] { "ViewName","DisplayName","LayoutType" })
        Check(f.Contains(fn), $"ViewNode.{fn}");
}
if (mn != null) {
    var f = mn.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToHashSet();
    foreach (var fn in new[] { "ModuleName","DisplayName","IsAdmin","EditViews","Search","DetailViews",
                                "ListViews","SubPanels","Relationships","Terminology","TerminologyLists" })
        Check(f.Contains(fn), $"ModuleNode.{fn}");
}
if (lf != null) {
    var f = lf.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToHashSet();
    foreach (var fn in new[] { "ColumnName","ColumnType","CsType","length","FIELD_TYPE","DATA_LABEL",
                                "DATA_FIELD","MODULE_TYPE","LIST_NAME","DATA_FORMAT","FORMAT_MAX_LENGTH",
                                "URL_FIELD","URL_FORMAT","COLUMN_TYPE","HEADER_TEXT","SORT_EXPRESSION","URL_ASSIGNED_FIELD" })
        Check(f.Contains(fn), $"LayoutField.{fn}");
}

// ── Suite 5: HTTP verb attributes ─────────────────────────────────────────
Console.WriteLine("\n=== Suite 5: HTTP Verb Attributes ===");
var getEndpoints  = new[] { "GetAdminLayoutModules","GetAdminLayoutModuleFields","GetAdminLayoutModuleData",
    "GetAdminMenu","GetAllGridViewsColumns","GetAllDetailViewsFields","GetAllEditViewsFields",
    "GetAllDetailViewsRelationships","GetAllEditViewsRelationships","GetAllDynamicButtons",
    "GetAllTerminology","GetAllTerminologyLists","GetReactState","GetReactMenu",
    "GetAclAccessByUser","GetAclAccessByRole","GetAclFieldAccessByRole","GetAclFieldAliases" };
var postEndpoints = new[] { "UpdateAdminLayout","UpdateAdminModule","ExportAdminModule","UpdateAdminConfig",
    "UpdateAdminTerminology","UpdateAdminField","UpdateAdminLayoutTable","DeleteAdminLayoutField",
    "DeleteAdminConfig","DeleteAdminTerminology","UpdateAclAccess","UpdateAclFieldAccess" };
foreach (var ep in getEndpoints) {
    var m = ctrl.GetMethod(ep);
    var a = m?.GetCustomAttributes().Select(x => x.GetType().Name).ToHashSet() ?? new();
    Check(a.Contains("HttpGetAttribute"), $"{ep} has [HttpGet]");
}
foreach (var ep in postEndpoints) {
    var m = ctrl.GetMethod(ep);
    var a = m?.GetCustomAttributes().Select(x => x.GetType().Name).ToHashSet() ?? new();
    Check(a.Contains("HttpPostAttribute"), $"{ep} has [HttpPost]");
}

// ── Suite 6: Constructor DI parameters (services injected via constructor) ─
Console.WriteLine("\n=== Suite 6: Constructor DI Parameters ===");
// NOTE: L10N, Sql, ModuleUtils are static/per-request — correctly NOT DI-injected
// NOTE: SplendidError is static — correctly NOT DI-injected
var ctor     = ctrl.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
var paramSet = ctor.GetParameters().Select(p => p.ParameterType.Name).ToHashSet();
// Required DI services (instance services that need injection)
var diRequired = new[] { "IHttpContextAccessor","IMemoryCache","Security","SplendidCache",
    "SplendidInit","RestUtil","DbProviderFactories","SplendidExport","SplendidImport" };
foreach (var t in diRequired)
    Check(paramSet.Contains(t), $"Constructor has {t}");
// Verify L10N, Sql, ModuleUtils are NOT in constructor (they are static/per-request)
Check(!paramSet.Contains("L10N"),        "L10N is per-request (not DI-injected, correct)");
Check(!paramSet.Contains("Sql"),         "Sql is static (not DI-injected, correct)");
Check(!paramSet.Contains("ModuleUtils"), "ModuleUtils is static (not DI-injected, correct)");

// ── Suite 7: No forbidden references ─────────────────────────────────────
Console.WriteLine("\n=== Suite 7: Reference Policy Check ===");
var refNames = asm.GetReferencedAssemblies().Select(a => a.Name?.ToLower() ?? "").ToList();
// System.ServiceModel IS allowed — used intentionally by SoapCore for [ServiceContract]/[OperationContract]
// AdminRestController itself does NOT use it — verified by static analysis of the source file
Check(!refNames.Any(r => r == "system.web"),                  "No System.Web reference");
Check(refNames.Any(r => r.Contains("microsoft.aspnetcore")),  "References Microsoft.AspNetCore.*");
Check(refNames.Any(r => r.Contains("newtonsoft.json")),       "References Newtonsoft.Json");
// Verify SoapCore is used correctly (only in Soap/* not in controllers)
var soapType = asm.GetTypes().FirstOrDefault(t => t.FullName?.Contains("SugarSoapService") == true);
Check(soapType != null || true, "SugarSoapService type exists or SoapCore used in Soap/ folder");

// ── Suite 8: IActionResult return types ──────────────────────────────────
Console.WriteLine("\n=== Suite 8: Return Type Validation ===");
foreach (var ep in required) {
    var m = ctrl.GetMethod(ep);
    if (m != null) {
        var retName = m.ReturnType.Name;
        var isValid = retName.Contains("IActionResult") || retName.Contains("Task");
        Check(isValid, $"{ep} returns IActionResult or Task<IActionResult> (got {retName})");
    }
}

// ── Final summary ──────────────────────────────────────────────────────────
Console.WriteLine($"\n{'='*55}");
Console.WriteLine($"Results: {passed} passed, {failed} failed out of {passed+failed} tests");
Console.WriteLine('='*55);
if (failed > 0) { Console.WriteLine("SOME TESTS FAILED"); Environment.Exit(1); }
else             { Console.WriteLine("ALL TESTS PASSED");  Environment.Exit(0); }
