# Directive 3 — API Surface Code Quality Audit

**Code Quality Assessment of Material API Surface Components per COSO Principles 10, 11, and 12**

---

#### Report Executive Summary

**Theme of Failure: "Monolithic API Gateways Without Validation Frameworks, Documentation, or Automated Testing Constitute a Systemic Breakdown of Control Activities"**

The API surface of SplendidCRM Community Edition v15.2 represents a **Critical-severity failure** of the COSO Control Activities component. Three coexisting API paradigms — a WCF REST gateway (`Rest.svc.cs`, 8,369 lines, ~82 public methods), a SugarCRM-compatible SOAP endpoint (`soap.asmx.cs`, 4,641 lines, ~41 web methods), and an Administration REST API (`Administration/Rest.svc.cs`, 6,473 lines, ~100 public members) — collectively expose approximately 19,483 lines of code as the primary attack surface of the application. Per COSO Principle 10 (Selects and Develops Control Activities), these API boundaries should implement the most rigorous input validation, authentication enforcement, and error containment controls in the entire system; instead, they rely on implicit `Sql.ToString()` / `Sql.ToGuid()` type-coercion wrappers as the sole input validation mechanism, with ASP.NET's built-in request validation deliberately disabled via `requestValidationMode="2.0"` in `Web.config`. Zero API documentation exists — no Swagger/OpenAPI specification, no maintained WSDL artifact, and no inline XML documentation comments — violating COSO Principle 14 (Communicates Internally) and rendering the API contract entirely opaque to both consumers and auditors. The complete absence of automated testing (zero unit tests, zero integration tests, zero API contract tests) across all three API paradigms constitutes a Critical deficiency under COSO Principle 16 (Conducts Ongoing and/or Separate Evaluations), as there is no mechanism to verify that Control Activities remain effective after code modifications.

Per COSO Principle 11 (Selects and Develops General Controls over Technology), the technology infrastructure supporting these APIs compounds the risk: all three WCF service contracts are decorated with `[ServiceBehavior(IncludeExceptionDetailInFaults=true)]`, which exposes internal exception details including stack traces and SQL queries in fault responses. Combined with `customErrors mode="Off"` in `Web.config`, the API surface may leak server-side implementation details to any caller. The `Impersonation.svc.cs` endpoint (58 lines) permits full user impersonation by administrators with minimal validation and — critically — no dedicated audit logging of impersonation events, creating an accountability gap under NIST AU-2 (Event Logging). The architectural pattern of consolidating all operations into single-file monoliths (the largest being 8,369 lines) produces extreme Single Responsibility Principle violations, excessive cyclomatic complexity, and cognitive load that increases the probability of security defects escaping review. Per COSO Principle 12 (Deploys through Policies and Procedures), the absence of any API governance framework — no rate limiting, no versioning strategy, no contract testing, no deprecation policies — leaves the Control Activities entirely dependent on individual developer discipline with no procedural guardrails.

---

#### Attention Required

| Component Path | Primary Finding | Risk Severity | Governing NIST/CIS Control | COSO Principle |
|---|---|---|---|---|
| `SplendidCRM/Rest.svc.cs` | 8,369-line monolithic API gateway — extreme SRP violation with ~82 public methods spanning 11 functional concerns | Critical | CIS Control 16 | Principle 10 |
| `SplendidCRM/Rest.svc.cs` | No input validation framework — relies on `Sql.ToString()` wrappers; `requestValidationMode="2.0"` disables built-in XSS protection | Critical | NIST SI-10 | Principle 11 |
| `SplendidCRM/Rest.svc.cs` | `IncludeExceptionDetailInFaults=true` combined with `customErrors="Off"` — exposes internal state in error responses | Critical | NIST SI-11 | Principle 11 |
| `SplendidCRM/Rest.svc.cs` | Zero API documentation — no Swagger/OpenAPI, no inline XML docs, no contract specification | Moderate | NIST SI-5 | Principle 14 |
| `SplendidCRM/Rest.svc.cs` | `Version()`, `Edition()`, `UtcTime()` endpoints accessible without authentication — information disclosure | Moderate | NIST AC-3 | Principle 11 |
| `SplendidCRM/Rest.svc.cs` | Zero automated tests for any endpoint — no unit, integration, or contract tests | Critical | NIST SI-2; CIS Control 16 | Principle 16 |
| `SplendidCRM/soap.asmx.cs` | Legacy SOAP endpoint — 4,641 lines with SugarCRM compatibility overhead; GUID-based session IDs cached in `HttpRuntime.Cache` | Moderate | NIST SC-8; NIST IA-3 | Principle 10 |
| `SplendidCRM/soap.asmx.cs` | Session ID exposed in error messages — `"The session ID is invalid. " + session` leaks session tokens in exceptions | Moderate | NIST SI-11 | Principle 11 |
| `SplendidCRM/Administration/Rest.svc.cs` | 6,473-line admin API monolith — privilege escalation risk surface with all admin operations in a single file | Critical | NIST AC-6 | Principle 10 |
| `SplendidCRM/Administration/Rest.svc.cs` | Inconsistent authorization checks — some methods enforce `IS_ADMIN` only, others accept `IS_ADMIN_DELEGATE` | Critical | NIST AC-6(1) | Principle 11 |
| `SplendidCRM/Administration/Impersonation.svc.cs` | User impersonation with no dedicated audit logging — impersonation events not recorded to SYSTEM_LOG | Critical | NIST AC-6(2); NIST AU-2 | Principle 11 |
| `SplendidCRM/Web.config` | `requestValidationMode="2.0"` disables .NET 4.5+ built-in request validation for the entire application | Critical | NIST SI-10 | Principle 11 |
| `SplendidCRM/Web.config` | `customErrors mode="Off"` exposes full stack traces and internal paths to remote clients | Critical | NIST SI-11 | Principle 11 |
| `SplendidCRM/Web.config` | `enableEventValidation="false"` and `validateRequest="false"` — additional XSS/injection protections disabled | Critical | NIST SI-10 | Principle 11 |
| `SplendidCRM/_code/RestUtil.cs` | 4,503-line serialization utility — `__metadata` objects expose internal URIs and type names in JSON responses | Moderate | NIST SI-11 | Principle 11 |

---

## Detailed Findings

### 1. Rest.svc.cs — WCF REST API Gateway

**system_id:** `SYS-API-REST`
**Classification:** Dynamic | **Materiality:** Material
**Lines of Code:** 8,369 | **Public Methods:** ~82 | **Private Methods:** ~12

Source: `SplendidCRM/Rest.svc.cs`

#### 1.1 Code Smells

##### 1.1.1 Method Length (>50 Lines) — Critical

The REST API gateway contains numerous methods significantly exceeding the 50-line threshold, indicating insufficient decomposition of complex operations.

| Method | Lines (approx.) | Line Range | Finding |
|---|---|---|---|
| `GetReactState()` | ~266 | L1017–L1283 | Mega-method assembling ALL React SPA bootstrap data — ACL, modules, layouts, terminology, relationships, users, teams, timezones, currencies, favorites, last-viewed. Single method returns the entire application state. |
| `MassUpdateModule()` | ~392 | L5184–L5576 | Handles mass-update across all modules with complex field iteration, team assignment normalization, and relationship management. |
| `ExportModuleList()` | ~298 | L2479–L2777 | Export pipeline combining query construction, ACL enforcement, data retrieval, and multi-format serialization. |
| `DeleteTableItems()` | ~181 | L7401–L7582 | Deletion orchestrator with relationship table discovery, cascade logic, and recurrence handling. |
| `UpdateRelatedItem()` (private) | ~168 | L5714–L5882 | Relationship update logic duplicated between public and private overloads with near-identical patterns. |
| `Login()` | ~170 | L351–L521 | Authentication dispatcher with 5 distinct authentication paths (ADFS, Azure AD, DuoUniversal, Windows, Forms). |
| `GetModuleList()` | ~111 | L1845–L1956 | Module list retrieval with OData-style query parsing, subquery prevention, and response serialization. |
| `GetModuleItem()` | ~635 | L2777–L3412 | Single-record retrieval with complex join logic for related data, multiple module-specific branches. |

Source: `SplendidCRM/Rest.svc.cs:L1017`

The `GetReactState()` method is the most extreme example, performing approximately 30 sequential cache lookups and serializing them into a single response dictionary. Each cache lookup follows an identical pattern of metrics tracking:

```csharp
results.Add("CONFIG", CONFIG);
metrics.Add("CONFIG", (DateTime.Now - dtStart).TotalSeconds);
```

Source: `SplendidCRM/Rest.svc.cs:L1080-L1081`

**Risk Severity:** Critical | **NIST:** SI-2 | **CIS:** Control 16 | **COSO:** Principle 10

##### 1.1.2 DRY Violations — Critical

Systematic repetition of identical code patterns across methods constitutes a significant DRY violation. The following patterns are repeated extensively:

**Pattern 1 — Cache Control Headers:** The following 3-line block appears in at least 20 methods across the file:

```csharp
HttpContext.Current.Response.ExpiresAbsolute = new DateTime(1980, 1, 1, 0, 0, 0, 0);
WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
```

Source: `SplendidCRM/Rest.svc.cs:L653-L655`

**Pattern 2 — Authentication + ACL Check:** The following pattern is repeated in virtually every data-access method with only the module name varying:

```csharp
int nACLACCESS = Security.GetUserAccess(ModuleName, "list");
if ( !Security.IsAuthenticated() || !Sql.ToBoolean(Application["Modules." + ModuleName + ".RestEnabled"]) || nACLACCESS < 0 )
```

Source: `SplendidCRM/Rest.svc.cs:L1872-L1873`

**Pattern 3 — JSON Serialization Boilerplate:** Response serialization follows an identical dictionary-wrapping pattern across all GET methods, assembling `results`, `d`, and `__count` keys.

**Pattern 4 — OData Parameter Extraction:** The `$skip`, `$top`, `$filter`, `$orderby`, `$select` parameter parsing block (~20 lines) is duplicated in `GetModuleList()`, `GetActivitiesList()`, `PostModuleList()`, and `ExportModuleList()`.

**Risk Severity:** Moderate | **NIST:** CM-3 | **CIS:** Control 16 | **COSO:** Principle 10

##### 1.1.3 Single Responsibility Principle Violations — Critical

`Rest.svc.cs` handles at least 11 distinct functional concerns within a single class, each of which warrants its own service boundary:

| Concern | Methods (approx.) | Line Regions |
|---|---|---|
| Scalar user properties | ~12 | L50–L320 |
| Authentication / Login | ~5 | L324–L643 |
| System layout retrieval | ~12 | L646–L947 |
| React state bootstrapping | ~3 | L950–L1315 |
| Module CRUD (list/item) | ~10 | L1317–L3786 |
| Calendar and invitees | ~4 | L3786–L4280 |
| Activity stream | ~4 | L4359–L4820 |
| Relationship insights | ~1 | L4820–L5059 |
| Update/create operations | ~12 | L5061–L7068 |
| Favorites/subscriptions | ~8 | L7070–L7319 |
| Delete operations | ~8 | L7321–L8099 |
| Sync and archive | ~4 | L8101–L8369 |

This extreme SRP violation means that any change to, for example, authentication logic risks unintended side effects in export functionality, relationship management, or calendar operations — all within the same compilation unit.

**Risk Severity:** Critical | **NIST:** CM-3 | **CIS:** Control 16 | **COSO:** Principle 10

##### 1.1.4 Deep Nesting (>3 Levels) — Moderate

Several methods exhibit nesting depths exceeding 3 levels. The `Login()` method at L351 demonstrates 5 levels of nesting through branching SSO paths:

```csharp
if ( !Sql.IsEmptyGuid(gUSER_ID) )  // Level 3
{   if ( resultTask.Result )        // Level 4 — nested within DuoUniversal enabled check (Level 1) and client ID check (Level 2)
```

Source: `SplendidCRM/Rest.svc.cs:L400-L414`

**Risk Severity:** Moderate | **NIST:** SI-2 | **COSO:** Principle 10

##### 1.1.5 Magic Numbers and Strings — Moderate

Hardcoded values appear throughout the API surface without named constants:

- `new DateTime(1980, 1, 1, 0, 0, 0, 0)` — cache expiration epoch, repeated 20+ times. Source: `SplendidCRM/Rest.svc.cs:L653`
- `json.MaxJsonLength = int.MaxValue` — disables JSON size limits, repeated in every serialization method. Source: `SplendidCRM/Rest.svc.cs:L219`
- `"ProductCatalog"`, `"Activities"`, `"Employees"`, `"ReportRules"` — hardcoded module name exceptions in `GetModuleList()`. Source: `SplendidCRM/Rest.svc.cs:L1858-L1868`

**Risk Severity:** Moderate | **NIST:** CM-6 | **COSO:** Principle 10

#### 1.2 Complexity Metrics

##### 1.2.1 Cyclomatic Complexity — Critical

| Method | Estimated CC | Threshold | Assessment |
|---|---|---|---|
| `Login()` | ~25 | >10 | 5 authentication paths (ADFS, Azure, Duo, Windows, Forms), each with multiple conditional branches for lockout, IP validation, and error handling |
| `GetReactState()` | ~15 | >10 | Sequential cache lookups with try/catch wrapping and conditional metrics |
| `GetModuleList()` | ~18 | >10 | Module name special-casing, ACL check, subquery prevention regex, OData parameter parsing |
| `MassUpdateModule()` | ~30+ | >10 | Field iteration, type checking, relationship handling, team normalization, all within nested loops |
| `GetModuleItem()` | ~35+ | >10 | Module-specific branching for different entity types, relationship loading, ACL filtering |
| `DeleteTableItems()` | ~20 | >10 | Relationship discovery, cascade decisions, recurrence handling |

**Risk Severity:** Critical | **NIST:** SI-2 | **CIS:** Control 16 | **COSO:** Principle 10

##### 1.2.2 Coupling Analysis — Critical

`Rest.svc.cs` exhibits efferent coupling to at least 14 distinct dependencies, substantially exceeding the 7-dependency threshold:

| Dependency | Category | Coupling Type |
|---|---|---|
| `HttpContext.Current` | Framework | Static accessor — thread-local ambient dependency |
| `HttpContext.Current.Application` | Framework | Shared application state |
| `HttpContext.Current.Session` | Framework | Per-request session state |
| `Security` | Internal | Static class — authentication, authorization, user context |
| `SplendidCache` | Internal | Static class — metadata, layouts, terminology, ACL |
| `SplendidInit` | Internal | Static class — login, IP validation, database build |
| `SplendidSession` | Internal | Static class — session persistence |
| `RestUtil` | Internal | Static class — JSON serialization, table access |
| `Sql` | Internal | Static class — type coercion, parameterization |
| `SplendidError` | Internal | Static class — error logging |
| `L10N` | Internal | Instance class — localization |
| `TimeZone` | Internal | Instance class — timezone conversion |
| `SqlProcs` | Internal | Static class — stored procedure wrappers |
| `DbProviderFactories` | Framework | Database provider factory |

**Total efferent coupling: 14 (threshold: 7)**

All internal dependencies are static classes accessed via static methods, creating hidden global state dependencies that are untestable without runtime infrastructure.

**Risk Severity:** Critical | **NIST:** CM-3; SC-5 | **CIS:** Control 16 | **COSO:** Principle 10

##### 1.2.3 Cognitive Complexity — Critical

At 8,369 lines with ~82 public methods organized into 9 `#region` blocks, the cognitive load required to understand `Rest.svc.cs` is extreme. A developer modifying any single method must hold mental context of the entire 8,369-line file to assess side effects, as shared state (HttpContext, Session, Application) is accessed freely throughout.

**Risk Severity:** Critical | **NIST:** SI-2 | **COSO:** Principle 10

##### 1.2.4 Cohesion — Critical

Cohesion is very low. The class combines authentication endpoints, metadata retrieval, CRUD operations, relationship management, synchronization, export/import, calendar operations, activity streams, favorites/subscriptions, and archive management. These represent at minimum 11 distinct bounded contexts that share no conceptual cohesion beyond "operations the REST API exposes."

**Risk Severity:** Critical | **NIST:** CM-3 | **COSO:** Principle 10

#### 1.3 Security Quality

##### 1.3.1 Input Validation — Critical

Input validation across all REST endpoints relies exclusively on the `Sql` utility class for type coercion:

- `Sql.ToString()` — converts input to string, does not validate content, format, or length
- `Sql.ToGuid()` — converts to GUID, returns `Guid.Empty` on failure (silent failure)
- `Sql.ToInteger()` — converts to integer, returns 0 on failure
- `Sql.ToDateTime()` — converts to DateTime
- `Sql.ToBoolean()` — converts to boolean

No explicit input validation framework is used. No maximum length enforcement, no whitelist validation, no format validation, no business rule validation exists at the API boundary. The `requestValidationMode="2.0"` setting in `Web.config` (L111) disables the .NET 4.5+ built-in request validation that would normally reject requests containing potentially dangerous HTML/script content.

Source: `SplendidCRM/Web.config:L111`

Additionally, `validateRequest="false"` and `enableEventValidation="false"` at `Web.config:L115` disable page-level request validation and event validation respectively:

```xml
<pages enableSessionState="true" enableEventValidation="false" validateRequest="false"
```

Source: `SplendidCRM/Web.config:L115`

A basic subquery prevention mechanism exists in `GetModuleList()` using regex keyword detection (L1891–L1908), but it operates on filter keywords after literal removal — a heuristic approach that may have bypass vectors.

**Risk Severity:** Critical | **NIST:** SI-10 | **CIS:** Control 16 | **COSO:** Principle 11

##### 1.3.2 Authentication Enforcement — Moderate

Authentication is enforced through `Security.IsAuthenticated()` checks at the beginning of most methods. However, the following endpoints are accessible without authentication:

- `Version()` — returns application version string. Source: `SplendidCRM/Rest.svc.cs:L53`
- `Edition()` — returns service level (Community/Professional/Enterprise). Source: `SplendidCRM/Rest.svc.cs:L62`
- `UtcTime()` — returns server UTC time. Source: `SplendidCRM/Rest.svc.cs:L70`
- `IsAuthenticated()` — returns authentication status. Source: `SplendidCRM/Rest.svc.cs:L78`
- `SingleSignOnSettings()` — returns SSO configuration including ADFS/Azure AD details. Source: `SplendidCRM/Rest.svc.cs:L281`
- `GetReactLoginState()` — returns login page configuration. Source: `SplendidCRM/Rest.svc.cs:L953`

While `Version()`, `Edition()`, and `UtcTime()` individually appear low-risk, collectively they enable fingerprinting of the application version, edition, and server clock — information useful for targeted attacks.

**Risk Severity:** Moderate | **NIST:** AC-3 | **CIS:** Control 6 | **COSO:** Principle 11

##### 1.3.3 Error Message Exposure — Critical

Three compounding factors create significant error message exposure risk:

1. **`[ServiceBehavior(IncludeExceptionDetailInFaults=true)]`** at L46 — WCF is configured to include full exception details (including stack traces, inner exceptions, and SQL query fragments) in SOAP fault responses.

2. **`customErrors mode="Off"`** at `Web.config:L51` — ASP.NET is configured to display detailed error pages including full stack traces, source code excerpts, and server configuration details to all clients (local and remote).

3. **`throw(new Exception(ex.Message))`** pattern — used in multiple catch blocks (e.g., L316), this re-throws with only the message but under `IncludeExceptionDetailInFaults=true`, the WCF infrastructure may still expose additional context.

Some error messages directly include user-supplied data:

```csharp
throw(new Exception("Unknown module: " + ModuleName));
```

Source: `SplendidCRM/Rest.svc.cs:L1870`

In addition, when `CONFIG.show_sql` is enabled, raw SQL queries are returned in API responses:

```csharp
dictResponse.Add("__sql", sbDumpSQL.ToString());
```

Source: `SplendidCRM/Rest.svc.cs:L1951`

**Risk Severity:** Critical | **NIST:** SI-11 | **CIS:** Control 16 | **COSO:** Principle 11

##### 1.3.4 CORS and Request Validation — Critical

The application disables multiple layers of ASP.NET built-in security protections:

| Setting | Location | Effect |
|---|---|---|
| `requestValidationMode="2.0"` | `Web.config:L111` | Disables .NET 4.5+ request validation; reverts to legacy 2.0 behavior that only validates ASPX pages, not WCF endpoints |
| `validateRequest="false"` | `Web.config:L115` | Disables request validation on all pages |
| `enableEventValidation="false"` | `Web.config:L115` | Disables event validation on all pages |

No CORS configuration was observed in `Web.config` or in the WCF service contracts. The `aspNetCompatibilityEnabled="true"` setting in `system.serviceModel` (L144) runs WCF in ASP.NET compatibility mode, meaning standard ASP.NET request pipeline applies — but with the protections above disabled, this pipeline provides minimal security value.

**Risk Severity:** Critical | **NIST:** SI-10; SC-8 | **COSO:** Principle 11

---

### 2. soap.asmx.cs — SugarCRM-Compatible SOAP API

**system_id:** `SYS-API-SOAP`
**Classification:** Dynamic | **Materiality:** Material
**Lines of Code:** 4,641 | **Web Methods:** ~41 | **Total Public Members:** ~167

Source: `SplendidCRM/soap.asmx.cs`

#### 2.1 Code Smells

##### 2.1.1 SRP Violation — Moderate

The single `soap` class handles all SOAP operations for the entire CRM: session management, entity CRUD, relationship operations, attachment handling, module metadata, search, and server information. The first ~478 lines consist entirely of data structure definitions (10 serializable classes), indicating the class also serves as a data contract definition layer.

**Risk Severity:** Moderate | **NIST:** CM-3 | **COSO:** Principle 10

##### 2.1.2 Method Length — Moderate

| Method | Lines (approx.) | Finding |
|---|---|---|
| `LoginUser()` | ~131 | L669–L800 — authentication with ADFS/Azure/Windows/Forms branching |
| `GetSessionUserID()` | ~68 | L569–L637 — session state management with cache manipulation |
| `get_entry_list()` (estimated) | ~100+ | Complex query construction with SugarCRM-compatible serialization |

Source: `SplendidCRM/soap.asmx.cs:L669`

##### 2.1.3 DRY Violations — Moderate

The SOAP `LoginUser()` method at L669 duplicates authentication logic present in `Rest.svc.cs:Login()`. Both implement the same 5-path authentication branching (ADFS, Azure, Windows, Forms, lockout check) independently rather than sharing a common authentication service.

The `GetSessionUserID()` method at L569 implements its own session cache management using `HttpRuntime.Cache` with a custom key scheme (`soap.session.user.{session}`, `soap.user.username.{userId}`, etc.), duplicating session state management that could be centralized.

**Risk Severity:** Moderate | **NIST:** CM-3 | **COSO:** Principle 10

##### 2.1.4 Legacy Overhead — Minor

The `sugarsoap` namespace declaration and `[SoapRpcMethod]` attributes maintain backward compatibility with SugarCRM clients. Data structure definitions (L89–L478) include SugarCRM-specific serialization types (`get_entry_list_result_encoded` with base64 XML encoding, `name_value` pairs, and PHP-compatible structures). This legacy surface area increases maintenance burden without modern security controls.

Source: `SplendidCRM/soap.asmx.cs:L51-L52`

**Risk Severity:** Minor | **NIST:** CM-7 | **COSO:** Principle 10

#### 2.2 Complexity Metrics

##### 2.2.1 Coupling — Moderate

The SOAP class couples to `HttpContext`, `HttpRuntime.Cache`, `Security`, `SplendidInit`, `SplendidCache`, `Sql`, `SplendidError`, `DbProviderFactories`, and `ActiveDirectory` — at least 9 direct dependencies, exceeding the 7-dependency threshold.

**Risk Severity:** Moderate | **NIST:** CM-3 | **COSO:** Principle 10

#### 2.3 Security Quality

##### 2.3.1 Session Management — Moderate

The SOAP API implements its own session management layer using `HttpRuntime.Cache` rather than ASP.NET session state. Session identifiers are GUIDs stored in the cache with a 24-hour sliding expiration (`DefaultCacheExpiration()` returns `DateTime.Now.AddDays(1)` at L566). The session ID is created during `login()` and must be passed as a parameter to subsequent calls.

Session state is stored under keys: `soap.session.user.{sessionGuid}`, `soap.user.username.{userId}`, `soap.user.currency.{userId}`, `soap.user.timezone.{userId}`, and `soap.user.expiration.{session}`.

Source: `SplendidCRM/soap.asmx.cs:L564-L566`

**Risk Severity:** Moderate | **NIST:** IA-3; SC-23 | **COSO:** Principle 11

##### 2.3.2 Session ID Exposure in Error Messages — Moderate

The `GetSessionUserID()` method includes the session identifier directly in the exception message when an invalid session is detected:

```csharp
throw(new Exception("The session ID is invalid.  " + session));
```

Source: `SplendidCRM/soap.asmx.cs:L597`

This may expose session tokens in error logs, SOAP fault responses, or client-side error handling, potentially aiding session hijacking.

**Risk Severity:** Moderate | **NIST:** SI-11; SC-23 | **COSO:** Principle 11

##### 2.3.3 WSDL Metadata Exposure — Minor

The WSDL is auto-generated by the ASP.NET runtime (`soap.asmx?WSDL`), exposing all method signatures, parameter types, and data structures to any client without authentication. The SugarCRM-compatible method names and data types reveal internal implementation details including module names, field structures, and available operations.

**Risk Severity:** Minor | **NIST:** SC-8 | **COSO:** Principle 11

##### 2.3.4 Unauthenticated Endpoints — Minor

The following SOAP methods are accessible without a session:

- `get_server_version()` — Source: `SplendidCRM/soap.asmx.cs:L485`
- `get_splendid_version()` — Source: `SplendidCRM/soap.asmx.cs:L493`
- `get_sugar_flavor()` — Source: `SplendidCRM/soap.asmx.cs:L502`
- `is_loopback()` — Source: `SplendidCRM/soap.asmx.cs:L518`
- `test(string s)` — echoes input back. Source: `SplendidCRM/soap.asmx.cs:L527`
- `get_server_time()` — Source: `SplendidCRM/soap.asmx.cs:L534`
- `get_gmt_time()` — Source: `SplendidCRM/soap.asmx.cs:L542`

The `test()` method at L527 echoes any input string back to the caller without validation or authentication, representing a potential reflection vector.

**Risk Severity:** Minor | **NIST:** AC-3 | **COSO:** Principle 11

---

### 3. Administration/Rest.svc.cs — Administration REST API

**system_id:** `SYS-API-ADMIN`
**Classification:** Dynamic | **Materiality:** Material
**Lines of Code:** 6,473 | **Public Methods/Members:** ~100

Source: `SplendidCRM/Administration/Rest.svc.cs`

#### 3.1 Code Smells

##### 3.1.1 SRP Violation — Critical

The Admin REST API consolidates all administration operations into a single class spanning 6,473 lines. The file handles:

- Layout CRUD (EditViews, DetailViews, GridViews, Relationships) — ~1,000+ lines
- Module management — several hundred lines
- Terminology and localization administration — ~200+ lines
- Configuration management — included via `PostAdminTable()`
- ACL role administration — integrated within admin table operations
- System diagnostics (view recompilation, audit rebuild) — ~200 lines
- Database archive management — ~40 lines
- Admin export functionality — ~200 lines

Additionally, the file defines 4 inner classes (`ViewNode`, `ModuleNode`, `LayoutField`, `RecompileStatus`) as data contracts at L48–L137, further violating SRP by mixing data structure definitions with service logic.

**Risk Severity:** Critical | **NIST:** CM-3 | **CIS:** Control 16 | **COSO:** Principle 10

##### 3.1.2 Method Length — Critical

The `GetAdminLayoutModules()` method at L108 spans approximately 227 lines (L108–L335), constructing a complex data hierarchy through multiple sequential SQL queries without decomposition. The method contains nested loops iterating over languages, modules, and layout types — all within a single method body.

Source: `SplendidCRM/Administration/Rest.svc.cs:L108`

##### 3.1.3 DRY Violations — Critical

The Admin REST API duplicates significant patterns from `Rest.svc.cs`:

- The cache-control header pattern is repeated across all GET methods
- Authentication + authorization check blocks are repeated in every public method
- JSON serialization and response wrapping patterns are duplicated
- The `PostAdminTable()` method at L1265 implements table-specific routing logic (~600+ lines) that parallels `Rest.svc.cs` CRUD operations

The `ExportAdminModule()` method at L1501 (~165 lines) duplicates export logic from `Rest.svc.cs:ExportModuleList()`.

**Risk Severity:** Critical | **NIST:** CM-3 | **COSO:** Principle 10

##### 3.1.4 Magic Numbers — Moderate

The SQL query strings in `GetAdminLayoutModules()` contain hardcoded language exclusions:

```sql
   and NAME not in ('en-AU', 'en-GB', 'en-CA')
```

Source: `SplendidCRM/Administration/Rest.svc.cs:L134`

These hardcoded locale exclusions bypass the module configuration system and are embedded directly in SQL strings.

**Risk Severity:** Moderate | **NIST:** CM-6 | **COSO:** Principle 10

#### 3.2 Complexity Metrics

##### 3.2.1 Cyclomatic Complexity — Critical

The `PostAdminTable()` method at L1265 serves as a dispatch router for all admin table update operations. It uses a long chain of `if/else if` conditions to route requests to specific table handlers (DYNAMIC_BUTTONS, EDITVIEWS_FIELDS, DETAILVIEWS_FIELDS, GRIDVIEWS_COLUMNS, EDITVIEWS, DETAILVIEWS, GRIDVIEWS, EDITVIEWS_RELATIONSHIPS, DETAILVIEWS_RELATIONSHIPS, and generic admin tables). Estimated cyclomatic complexity exceeds 30.

**Risk Severity:** Critical | **NIST:** SI-2 | **COSO:** Principle 10

##### 3.2.2 Coupling — Critical

The Admin REST API couples to: `HttpContext`, `Security`, `SplendidCache`, `SplendidSession`, `RestUtil`, `Sql`, `SplendidError`, `L10N`, `DbProviderFactories`, `SqlBuild`, `SqlProcs`, `SplendidDynamic`, plus direct SQL via `IDbCommand`. Total efferent coupling: ~13 dependencies (threshold: 7).

**Risk Severity:** Critical | **NIST:** CM-3 | **COSO:** Principle 10

#### 3.3 Security Quality

##### 3.3.1 IS_ADMIN Enforcement — Inconsistent Authorization Pattern — Critical

Audit of the `IS_ADMIN` enforcement pattern across the Admin REST API reveals **two distinct authorization patterns** used inconsistently:

**Pattern A — Strict IS_ADMIN only (11 instances):**
```csharp
if ( !Security.IsAuthenticated() || !Security.IS_ADMIN )
```

Source: `SplendidCRM/Administration/Rest.svc.cs:L116`

This pattern appears at lines: L116, L345, L716, L799, L903, L1149, L1185, L1208, L1230.

**Pattern B — IS_ADMIN or IS_ADMIN_DELEGATE (6 instances):**
```csharp
if ( !Security.IsAuthenticated() || !(Security.IS_ADMIN || SplendidCRM.Security.IS_ADMIN_DELEGATE) )
```

Source: `SplendidCRM/Administration/Rest.svc.cs:L1298`

This pattern appears at lines: L1298, L1838, L2209, L2358, L2465, L2964.

The inconsistency between these two patterns means that some admin operations require full `IS_ADMIN` status while others accept delegated admin access, but this distinction is not documented or governed by a clear policy. Methods using Pattern A at L116 (layout retrieval) require stricter access than methods using Pattern B at L1298 (admin table posting), which appears inverted — data modification operations should require equal or greater privilege than read operations.

Additionally, within Pattern B methods, further granular checks using `SplendidCRM.Security.AdminUserAccess()` are applied per module (e.g., L1379, L1415, L1455), creating a complex multi-layered authorization model that is difficult to audit for completeness.

**Risk Severity:** Critical | **NIST:** AC-6; AC-6(1) | **CIS:** Control 6 | **COSO:** Principle 11

##### 3.3.2 Admin Action Logging — Moderate

Admin operations route through standard CRUD stored procedures via `RestUtil.UpdateTable()` and `SqlProcs` wrappers, which set `MODIFIED_USER_ID` parameters:

```csharp
IDbDataParameter parMODIFIED_USER_ID = Sql.AddParameter(cmd, "@MODIFIED_USER_ID", Security.USER_ID);
```

Source: `SplendidCRM/Administration/Rest.svc.cs:L1250`

This provides basic modification tracking via the database `MODIFIED_USER_ID` column and audit triggers. However, there is no dedicated admin action logging to `SYSTEM_LOG` for sensitive operations like view recompilation (`RecompileViews()` at L1180), audit rebuild (`RebuildAudit()` at L1203), or archive management (`BuildModuleArchive()` at L1225).

**Risk Severity:** Moderate | **NIST:** AU-2 | **CIS:** Control 8 | **COSO:** Principle 16

##### 3.3.3 Error Handling — Moderate

The `IncludeExceptionDetailInFaults=true` attribute at L41 applies to the Admin REST API as well. Admin-specific errors may expose configuration paths, module structures, and SQL schema information to any user with admin access (or anyone who can trigger admin endpoint errors).

Source: `SplendidCRM/Administration/Rest.svc.cs:L41`

**Risk Severity:** Moderate | **NIST:** SI-11 | **COSO:** Principle 11

---

### 4. Impersonation.svc.cs — User Impersonation Service

**system_id:** `SYS-API-ADMIN`
**Classification:** Dynamic | **Materiality:** Material
**Lines of Code:** 58 | **Public Methods:** 1

Source: `SplendidCRM/Administration/Impersonation.svc.cs`

#### 4.1 Functionality Assessment — Critical

The `Impersonate(Guid ID)` method at L39 allows an administrator to assume the identity of any user in the system. The complete method implementation performs the following operations:

1. **Authorization Check:** Verifies `Security.IsAuthenticated()` AND `Security.IS_ADMIN` at L45.
2. **Feature Toggle:** Checks physical file existence of `~/Administration/Impersonation.svc` at L49 — the endpoint's own file serves as the feature toggle. If the file is deleted from the filesystem, impersonation is disabled.
3. **Identity Switch:** Calls `SplendidInit.LoginUser(ID, "Impersonate")` at L54 to replace the current session with the target user's identity.
4. **Flag Setting:** Sets `Session["USER_IMPERSONATION"] = true` at L55.

Source: `SplendidCRM/Administration/Impersonation.svc.cs:L39-L56`

#### 4.2 Authorization Check Assessment — Moderate

The authorization check at L45 correctly requires both authentication and `IS_ADMIN` status:

```csharp
if ( !Security.IsAuthenticated() || !Security.IS_ADMIN )
```

Source: `SplendidCRM/Administration/Impersonation.svc.cs:L45`

This uses Pattern A (strict IS_ADMIN), which is appropriate for this high-risk operation. The `IS_ADMIN_DELEGATE` access is correctly excluded, limiting impersonation to full administrators only.

**Risk Severity:** Moderate (adequate but minimal) | **NIST:** AC-6(2) | **COSO:** Principle 11

#### 4.3 Audit Logging Gap — Critical

The impersonation operation has **no dedicated audit logging**. The method calls `SplendidInit.LoginUser()` which may generate a standard login event, but there is no explicit logging that records:

- Which administrator initiated the impersonation
- Which target user is being impersonated
- The timestamp of the impersonation event
- The IP address of the administrator performing the impersonation

The `Session["USER_IMPERSONATION"] = true` flag is set but there is no evidence of this flag being consumed by the audit trail system. After `SplendidInit.LoginUser(ID, "Impersonate")` executes, the session context switches to the target user — meaning the original administrator's identity may be lost for subsequent audit entries. This creates a critical accountability gap: all actions performed during impersonation may appear to have been performed by the target user, not the administrator.

**Risk Severity:** Critical | **NIST:** AU-2; AU-3; AC-6(2) | **CIS:** Control 8 | **COSO:** Principle 11

#### 4.4 Physical File Feature Toggle — Minor

Using the physical existence of the `.svc` file as a feature toggle mechanism (L49) is an unconventional approach. This means:

- The feature state is determined by filesystem state, not by configuration data
- There is no audit trail for enabling/disabling impersonation
- File system permissions become the access control mechanism for this feature toggle
- Deployment processes could inadvertently enable/disable impersonation

```csharp
if ( !File.Exists(Server.MapPath("~/Administration/Impersonation.svc")) )
```

Source: `SplendidCRM/Administration/Impersonation.svc.cs:L49`

**Risk Severity:** Minor | **NIST:** CM-6 | **COSO:** Principle 10

---

### 5. RestUtil.cs — REST Utility Support Layer

**system_id:** `SYS-API-REST`
**Classification:** Dynamic | **Materiality:** Material
**Lines of Code:** 4,503 | **Public Methods:** ~23

Source: `SplendidCRM/_code/RestUtil.cs`

#### 5.1 Role Assessment

`RestUtil.cs` serves as the shared utility layer for REST JSON serialization, date/timezone conversion, ACL-aware table selection, and CRUD dispatching. It was created by extracting common patterns from `Rest.svc.cs` (as noted in comments: "12/16/2019 Paul. Moved GetTable to ~/_code/RestUtil.cs").

#### 5.2 Code Smells

##### 5.2.1 Method Length — Moderate

The `ToJson()` overloads and `RowsToDictionary()` method at L153 contain complex column iteration logic with type-based branching for date conversion. The `GetTable()` method (which performs ACL-filtered data retrieval) is estimated at 200+ lines based on the file size and method count.

##### 5.2.2 DRY Violations — Moderate

The `ToJson()` method has two overloads (L88 for DataTable, L101 for DataRow) that contain significant code duplication in the column iteration and metadata construction logic. The `__metadata` dictionary construction at L121–L129 and L176–L186 is nearly identical between the two overloads.

**Risk Severity:** Moderate | **NIST:** CM-3 | **COSO:** Principle 10

#### 5.3 Coupling Assessment — Moderate

`RestUtil.cs` couples to: `HttpContext`, `Security`, `SplendidCache`, `Sql`, `SplendidError`, `L10N`, `TimeZone`, `DbProviderFactories`, `SqlProcs`. Total coupling: ~9 dependencies (threshold: 7).

**Risk Severity:** Moderate | **NIST:** CM-3 | **COSO:** Principle 10

#### 5.4 Security Quality

##### 5.4.1 Information Leakage via __metadata — Moderate

The `ToJson()` methods construct `__metadata` objects containing internal URIs and type names in every JSON response:

```csharp
metadata.Add("uri", sBaseURI + "?ModuleName=" + sModuleName + "&ID=" + gID.ToString());
metadata.Add("type", "SplendidCRM." + sModuleName);
```

Source: `SplendidCRM/_code/RestUtil.cs:L122-L123`

This exposes internal module names, entity identifiers, and server-side type information in every API response, providing attack surface enumeration to any authenticated user.

**Risk Severity:** Moderate | **NIST:** SI-11 | **COSO:** Principle 11

##### 5.4.2 JSON Serialization Configuration — Minor

The `JavaScriptSerializer` is used with `MaxJsonLength = int.MaxValue` throughout the API surface. No custom deserialization type restrictions are configured, which in older ASP.NET versions could contribute to deserialization vulnerabilities. The `ToJsonStream()` helper method returns raw UTF-8 byte streams without content-type headers being explicitly set at this layer.

**Risk Severity:** Minor | **NIST:** SI-10 | **COSO:** Principle 11

---

### 6. Web.config — API Security Configuration

**system_id:** `SYS-CONFIG` / `SYS-IIS-CFG`
**Classification:** Static | **Materiality:** Material

Source: `SplendidCRM/Web.config`

While `Web.config` is assessed in detail in the [Security Domain Quality Report](./security-domain-quality.md), the following findings are directly relevant to the API surface quality assessment:

| Setting | Line | Value | Impact on API Surface |
|---|---|---|---|
| `requestValidationMode` | L111 | `"2.0"` | Disables .NET 4.5+ request validation for ALL endpoints including WCF REST and SOAP |
| `customErrors mode` | L51 | `"Off"` | Exposes detailed error information including stack traces to ALL remote clients |
| `validateRequest` | L115 | `"false"` | Disables page-level request validation |
| `enableEventValidation` | L115 | `"false"` | Disables event validation — reduces CSRF protection |
| `sessionState mode` | L100 | `"InProc"` | In-process session state — API session affinity required; no distributed support |
| `maxRequestLength` | L111 | `104857600` (100MB) | Permits very large request bodies — potential DoS vector |
| `executionTimeout` | L111 | `600` (10 min) | Long execution timeout — amplifies potential DoS impact |
| `maxReceivedMessageSize` | L149 | `2147483647` (~2GB) | WCF binding allows up to 2GB messages — extreme DoS vector |

Source: `SplendidCRM/Web.config:L111`, `SplendidCRM/Web.config:L149`

**Risk Severity:** Critical (cumulative) | **NIST:** SI-10; SI-11; SC-5 | **COSO:** Principle 11

---

## Summary Statistics

### API Surface Domain — Aggregate Metrics

| Component | system_id | Lines of Code | Public Methods | Code Smells Found | Complexity Rating | Security Issues | Overall Risk |
|---|---|---|---|---|---|---|---|
| `Rest.svc.cs` | `SYS-API-REST` | 8,369 | ~82 | 5 Critical (SRP, DRY, method length, nesting, magic numbers) | Critical (CC >25 in multiple methods; coupling 14) | 4 Critical (input validation, error exposure, no tests, disabled protections) | **Critical** |
| `soap.asmx.cs` | `SYS-API-SOAP` | 4,641 | ~41 | 3 Moderate (SRP, DRY, method length) | Moderate (coupling 9) | 2 Moderate (session mgmt, session ID in errors), 2 Minor | **Moderate** |
| `Admin/Rest.svc.cs` | `SYS-API-ADMIN` | 6,473 | ~100 | 4 Critical (SRP, DRY, method length, magic numbers) | Critical (CC >30; coupling 13) | 2 Critical (inconsistent IS_ADMIN, IncludeExceptionDetail), 1 Moderate (logging) | **Critical** |
| `Admin/Impersonation.svc.cs` | `SYS-API-ADMIN` | 58 | 1 | None (compact) | Low | 1 Critical (no audit logging), 1 Moderate (minimal validation), 1 Minor (file toggle) | **Critical** |
| `RestUtil.cs` | `SYS-API-REST` | 4,503 | ~23 | 2 Moderate (DRY, method length) | Moderate (coupling 9) | 1 Moderate (metadata leakage), 1 Minor (serialization config) | **Moderate** |

### Totals

| Metric | Value |
|---|---|
| **Total Lines of Code (API Surface)** | 24,044 |
| **Total Public Methods** | ~247 |
| **Critical Findings** | 14 |
| **Moderate Findings** | 12 |
| **Minor Findings** | 5 |
| **Components Rated Critical Risk** | 3 of 5 |
| **Components with Zero Tests** | 5 of 5 (100%) |
| **API Documentation Coverage** | 0% |

### Framework Compliance Summary

| Framework | Control | Assessment | Status |
|---|---|---|---|
| COSO Principle 10 | Selects and Develops Control Activities | API boundaries lack validation frameworks, testing, and decomposition | **Deficient** |
| COSO Principle 11 | Selects and Develops General Controls over Technology | Error exposure, disabled protections, inconsistent authorization | **Deficient** |
| COSO Principle 12 | Deploys through Policies and Procedures | No API governance, no versioning, no rate limiting, no deprecation policies | **Deficient** |
| COSO Principle 14 | Communicates Internally | Zero API documentation across all paradigms | **Deficient** |
| COSO Principle 16 | Conducts Ongoing Evaluations | Zero automated testing for any endpoint | **Deficient** |
| NIST SI-10 | Information Input Validation | Request validation disabled; no input validation framework | **Not Implemented** |
| NIST SI-11 | Error Handling | Stack traces exposed; session IDs in errors; metadata leakage | **Not Implemented** |
| NIST AC-3 | Access Enforcement | Unauthenticated endpoints exist; inconsistent admin checks | **Partially Implemented** |
| NIST AC-6 | Least Privilege | Inconsistent IS_ADMIN vs IS_ADMIN_DELEGATE enforcement | **Partially Implemented** |
| NIST AU-2 | Event Logging | Impersonation events not logged; admin operations partially tracked | **Partially Implemented** |
| NIST SC-5 | Denial of Service Protection | 2GB max message size; 10-min timeout; no rate limiting | **Not Implemented** |
| CIS Control 16 | Application Software Security | No input validation, no testing, no security review evidence | **Not Implemented** |
| CIS Control 6 | Access Control Management | Authorization inconsistencies in admin API | **Partially Implemented** |
| CIS Control 8 | Audit Log Management | Critical admin operations (impersonation) lack audit trail | **Partially Implemented** |

---

*All findings in this report are attributed to system_ids from the [System Registry](../directive-0-system-registry/system-registry.md). Materiality classification is per the [Materiality Classification Report](../directive-2-materiality/materiality-classification.md). This report is an assess-only audit artifact — no code has been created, modified, or remediated.*
