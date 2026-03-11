# Directive 0 — COSO Internal Control Framework Mapping

## Introduction

This document maps each system registered in the SplendidCRM Community Edition v15.2 System Registry ([system-registry.md](./system-registry.md)) to the 17 Principles of the COSO Internal Control — Integrated Framework (2013), organized across the five COSO components: Control Environment, Risk Assessment, Control Activities, Information & Communication, and Monitoring Activities. The COSO 2013 framework requires that all 17 principles be both **present** (evidence of design exists) and **functioning** (evidence of operating effectiveness exists) for internal controls to be deemed effective. A deficiency in any single principle can undermine the entire control structure.

This mapping forms the **governance anchor** for the entire SplendidCRM codebase audit. All subsequent directives — from Directive 1 (Structural Integrity) through Directive 8 (Risk Executive Presentation) — reference COSO principles when characterizing findings, attributing risk severity, and prioritizing remediation. The `system_id` identifiers used throughout this document are drawn exclusively from the authoritative registry in [system-registry.md](./system-registry.md), which decomposes SplendidCRM into 34 discrete systems across 13 functional domains and 9 architectural layers.

**Assessment Methodology:** For each of the 17 COSO principles, this mapping evaluates two dimensions based on static code analysis of the SplendidCRM codebase: (1) **Present** — whether evidence of control design exists in the source code, configuration, or project artifacts, and (2) **Functioning** — whether the control operates effectively as designed, based on code paths, configuration state, and architectural patterns observed. Only controls that are actually observed in the codebase are documented; aspirational controls that should exist but do not are reported as gaps, not as implemented controls.

### Assessment Rating Definitions

| Rating | Definition | Criteria |
|---|---|---|
| **Effective** | Principle is both present and functioning | Control is designed and operates as intended with no material gaps |
| **Partially Effective** | Principle is present but functioning with gaps | Control design exists but effectiveness is undermined by specific deficiencies |
| **Deficient** | Principle is present but not functioning effectively | Control exists in limited form but fails to achieve its objective |
| **Not Implemented** | No evidence the principle is addressed | No observable control design or operating effectiveness |

### Risk Severity Classification

Findings associated with each COSO principle are classified using the following severity levels:

| Severity | Definition |
|---|---|
| **Critical** | Gap directly undermines security, data integrity, or operational reliability |
| **Moderate** | Gap reduces control effectiveness but does not create immediate operational risk |
| **Minor** | Gap represents a best-practice deviation with limited operational impact |

---

## Component 1 — Control Environment

The Control Environment component of the COSO framework establishes the organizational tone for internal control and provides the foundation upon which the other four components operate. In the context of a codebase audit, the Control Environment translates to the existence of coding standards, ethical commitments (licensing), governance structures for code changes, competency requirements for developers, and accountability mechanisms for both application users and code contributors. Per COSO Principle 1 through COSO Principle 5, these foundational elements determine whether the organization has created conditions conducive to effective internal control.

### COSO Principle 1 — Demonstrates Commitment to Integrity and Ethical Values

COSO Principle 1 requires the organization to demonstrate a commitment to integrity and ethical values. In a codebase context, this manifests as licensing compliance, open-source ethical commitments, contributor conduct policies, and security disclosure mechanisms.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/Security.cs:L1-21`). No security disclosure policy. |
| `SYS-AUTH-AD` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/ActiveDirectory.cs:L1-21`). |
| `SYS-AUTH-DUO` | Yes | Partial | Partially Effective | AGPLv3 license headers on DuoUniversal source files. |
| `SYS-API-REST` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/Rest.svc.cs`). |
| `SYS-API-SOAP` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/soap.asmx.cs`). |
| `SYS-API-ADMIN` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/Administration/Rest.svc.cs`). |
| `SYS-CACHE` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/SplendidCache.cs`). |
| `SYS-INIT` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/Global.asax.cs:L1-21`). |
| `SYS-DB-ACCESS` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/Sql.cs`). |
| `SYS-ERROR-OBSERVABILITY` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/SplendidError.cs:L1-21`). |
| `SYS-BUSINESS-LOGIC` | Yes | Partial | Partially Effective | AGPLv3 headers consistent across all 40 CRM module folders. |
| `SYS-IMPORT-EXPORT` | Yes | Partial | Partially Effective | AGPLv3 license headers on import/export utility classes. |
| `SYS-CAMPAIGN` | Yes | Partial | Partially Effective | AGPLv3 license headers on campaign processing files. |
| `SYS-EMAIL` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/EmailUtils.cs`). |
| `SYS-SMS-TELEPHONY` | Yes | Partial | Partially Effective | AGPLv3 license headers on Twilio integration files. |
| `SYS-REALTIME` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs:L1-21`). |
| `SYS-SCHEDULER` | Yes | Partial | Partially Effective | AGPLv3 license header present (Source: `SplendidCRM/_code/SchedulerUtils.cs`). |
| `SYS-WORKFLOW` | Yes | Partial | Partially Effective | AGPLv3 license headers on workflow stub files. |
| `SYS-CONFIG` | Yes | Partial | Partially Effective | Configuration files lack license headers but serve AGPLv3-licensed application. |
| `SYS-ADMIN` | Yes | Partial | Partially Effective | AGPLv3 license headers across 45 admin sub-modules. |
| `SYS-REPORTING` | Yes | Partial | Partially Effective | AGPLv3 license headers on reporting utility files. |
| `SYS-CONTENT` | Yes | Partial | Partially Effective | AGPLv3 license headers on document/note management files. |
| `SYS-L10N` | Yes | Partial | Partially Effective | AGPLv3 license headers on localization files. |
| `SYS-INTEGRATION-STUBS` | Yes | Partial | Partially Effective | AGPLv3 license headers on all 16+ integration stub directories. |
| `SYS-SQL-DB` | Yes | Partial | Partially Effective | AGPLv3 license headers on all SQL script files (Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql:L1-5`). |
| `SYS-AUDIT` | Yes | Partial | Partially Effective | AGPLv3 license header present on trigger generation script. |
| `SYS-ASPNET-APP` | Yes | Partial | Partially Effective | AGPLv3 license headers on application host files. |
| `SYS-IIS-CFG` | — | — | — | Not applicable — `Web.config` is a configuration file, not a copyrightable work requiring licensing. |
| `SYS-REACT-SPA` | Yes | Partial | Partially Effective | `package.json` declares license but no contributor guidelines (Source: `SplendidCRM/React/package.json`). |
| `SYS-ANGULAR-CLIENT` | Yes | Partial | Partially Effective | `package.json` present (Source: `SplendidCRM/Angular/package.json`). |
| `SYS-HTML5-CLIENT` | Yes | Partial | Partially Effective | AGPLv3 licensing on HTML5 client source files. |
| `SYS-WEBFORMS` | Yes | Partial | Partially Effective | AGPLv3 license headers on WebForms code-behind files. |
| `SYS-BUILD-PIPELINE` | Yes | Partial | Partially Effective | Build scripts exist within AGPLv3-licensed repository. |
| `SYS-DEPENDENCY-MGMT` | Yes | Partial | Partially Effective | DLLs referenced from `BackupBin2012/` — third-party licensing not fully documented. |

**Summary Assessment: Partially Effective** (Risk Severity: Moderate)

AGPLv3 licensing is consistently applied across all source files — license headers appear in every C# source file (Source: `SplendidCRM/_code/Security.cs:L1-21`, `SplendidCRM/Global.asax.cs:L1-21`), all SQL scripts, and the `README.md` acknowledges open-source obligations. This demonstrates a genuine commitment to open-source ethical values per COSO Principle 1. However, the commitment is incomplete: no `CODE_OF_CONDUCT.md`, no `CONTRIBUTING.md`, no `SECURITY.md` security disclosure policy, and no contributor license agreement exist in the repository. Third-party DLL licensing in `BackupBin2012/` is not formally documented beyond XML IntelliSense files.

---

### COSO Principle 2 — Exercises Oversight Responsibility

COSO Principle 2 requires the board or oversight body to demonstrate independence from management and exercise oversight of the development and performance of internal control. For a codebase, this translates to code review processes, approval workflows, and governance structures for code changes.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| All 34 systems | No | No | Deficient | No code review process, no pull request workflow, no approval gates observed across the entire codebase. |

**Summary Assessment: Deficient** (Risk Severity: Critical)

No evidence of any oversight mechanism exists within the SplendidCRM codebase per COSO Principle 2. There is no `.github/` directory containing pull request templates or workflow configurations, no code review tooling integration, no approval gates for configuration changes in `SYS-CONFIG` or `SYS-IIS-CFG`, and no branch protection rules. The copyright attribution across all files references a single entity ("SplendidCRM Software, Inc.") with inline developer comments attributed to a single developer ("Paul"). While single-developer projects can have effective controls, the complete absence of any formalized oversight mechanism — even a documented self-review checklist — constitutes a deficiency in COSO Principle 2.

---

### COSO Principle 3 — Establishes Structure, Authority, and Responsibility

COSO Principle 3 requires that management establish, with board oversight, structures, reporting lines, and appropriate authorities and responsibilities in the pursuit of objectives. In a codebase, this maps to clear architectural boundaries, role-based access control structures, and separation of duties.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | Yes | Yes | Partially Effective | `IS_ADMIN` / `IS_ADMIN_DELEGATE` role structure enforces authority delineation (Source: `SplendidCRM/_code/Security.cs:L137-167`). 4-tier authorization model establishes clear responsibility boundaries. |
| `SYS-API-ADMIN` | Yes | Yes | Partially Effective | `IS_ADMIN` enforcement on all admin API endpoints separates admin from user authority (Source: `SplendidCRM/Administration/Rest.svc.cs`). |
| `SYS-ADMIN` | Yes | Yes | Partially Effective | 45 admin sub-modules with distinct functional responsibilities. |
| `SYS-CONFIG` | Yes | Partial | Partially Effective | Configuration structure exists but lacks change control governance. |
| `SYS-IIS-CFG` | Yes | Partial | Partially Effective | Web.config provides structured IIS configuration but no deployment authority controls. |
| `SYS-INIT` | Yes | Yes | Partially Effective | Bootstrap sequence clearly structured in `Application_Start` → timer initialization chain (Source: `SplendidCRM/Global.asax.cs:L92`). |
| `SYS-ASPNET-APP` | Yes | Yes | Partially Effective | Clear separation of application host, API endpoints, and infrastructure layers. |
| Other systems | Partial | Partial | Partially Effective | Functional domain separation (13 domains) and architectural layering (9 layers) provide structural clarity. |

**Summary Assessment: Partially Effective** (Risk Severity: Moderate)

COSO Principle 3 is partially effective at the application level. The codebase demonstrates clear structural organization: the `_code/` directory contains infrastructure utilities, `Administration/` houses admin modules, 40 CRM module folders implement business logic, and `SQL Scripts Community/` manages the database layer. The 4-tier authorization model in `SYS-SECURITY` (module ACL → team filtering → field-level → record-level) provides strong authority delineation within the running application. However, organizational structure for code governance — including code ownership, review responsibilities, and change authority — is entirely absent. Per COSO Principle 3, structure exists at the technical architecture level but not at the governance level.

---

### COSO Principle 4 — Demonstrates Commitment to Competence

COSO Principle 4 requires the organization to demonstrate a commitment to attract, develop, and retain competent individuals in alignment with objectives. For a codebase, this translates to evidence of technical competence in implementation, coding standards, developer onboarding documentation, and skills development.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | Yes | Partial | Partially Effective | Complex 4-tier authorization implementation demonstrates high competence. No coding guidelines documented. |
| `SYS-CACHE` | Yes | Partial | Partially Effective | Thousands of metadata query getters demonstrate deep domain knowledge. |
| `SYS-DB-ACCESS` | Yes | Partial | Partially Effective | Provider-agnostic database layer with parameterized queries demonstrates competent architecture. |
| `SYS-SQL-DB` | Yes | Partial | Partially Effective | 833 stored procedures and 581 views demonstrate extensive SQL expertise. |
| `SYS-REACT-SPA` | Yes | Partial | Partially Effective | Modern React 18.2.0 / TypeScript 5.3.3 stack demonstrates current technology competence. |
| All other systems | Yes | Partial | Partially Effective | Multi-technology stack (C#/.NET 4.8, TypeScript, Angular, jQuery, T-SQL) maintained over 20 years (2005–2025). |

**Summary Assessment: Partially Effective** (Risk Severity: Moderate)

COSO Principle 4 shows strong evidence of technical competence through implementation quality. The codebase spans multiple complex technology stacks — C#/.NET 4.8, TypeScript/React 18.2.0, Angular ~13.3.0, jQuery/RequireJS, and T-SQL — all maintained across a 20-year development history. The sophistication of systems like `SYS-SECURITY` (4-tier authorization), `SYS-CACHE` (thousands of metadata accessors), and `SYS-SQL-DB` (833 stored procedures) demonstrates sustained high-level expertise. However, no documentation of technical standards, no coding guidelines, no developer onboarding documentation, and no `CONTRIBUTING.md` exist. The `README.md` (78 lines) covers only build prerequisites, not development practices. Per COSO Principle 4, competence is evident in the code but the competence framework is undocumented.

---

### COSO Principle 5 — Enforces Accountability

COSO Principle 5 requires the organization to hold individuals accountable for their internal control responsibilities. In a codebase context, this translates to audit trails for application users, accountability mechanisms for code contributors, and enforcement of access control policies.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | Yes | Yes | Partially Effective | `IS_ADMIN` enforcement prevents unauthorized privilege escalation. User session tracking via `USER_LOGIN_ID` (Source: `SplendidCRM/_code/Security.cs:L72-86`). |
| `SYS-AUDIT` | Yes | Yes | Partially Effective | SQL audit triggers track entity-level INSERT/UPDATE/DELETE changes with user attribution and timestamps (Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql`). |
| `SYS-ERROR-OBSERVABILITY` | Yes | Yes | Partially Effective | `SplendidError.cs` logs system events to `SYSTEM_LOG` with user, session, and source attribution via `spSYSTEM_LOG_InsertOnly` (Source: `SplendidCRM/_code/SplendidError.cs:L239`). |
| `SYS-SQL-DB` | Yes | Partial | Partially Effective | `USERS_LOGINS` table tracks authentication events. Audit tables (`*_AUDIT`) track entity changes. |
| `SYS-API-ADMIN` | Yes | Yes | Partially Effective | Admin operations enforce `IS_ADMIN` checks. User impersonation logged (Source: `SplendidCRM/Administration/Impersonation.svc.cs`). |
| `SYS-BUILD-PIPELINE` | No | No | Deficient | No git hooks, no CI/CD gates, no build-time accountability checks. |
| `SYS-DEPENDENCY-MGMT` | No | No | Deficient | No automated dependency audit, no SBOM generation. |
| All other systems | Partial | Partial | Partially Effective | Application-level audit trails present; development process accountability absent. |

**Summary Assessment: Partially Effective** (Risk Severity: Moderate)

COSO Principle 5 shows a clear split between application-level and development-level accountability. Application-level accountability is well-implemented: `SYS-AUDIT` provides comprehensive entity-level audit triggers, `SYS-ERROR-OBSERVABILITY` logs all system events with user attribution, and `SYS-SECURITY` enforces role-based access with session tracking. However, development process accountability is entirely absent — no git hooks enforce review requirements, no CI/CD pipeline gates require approval, and no change tracking exists for configuration or dependency modifications. This gap means that while the application can hold its users accountable, there is no mechanism to hold code contributors accountable for changes to the codebase itself.

---

## Component 2 — Risk Assessment

The Risk Assessment component of the COSO framework involves identifying and analyzing risks to achieving objectives. Per COSO Principle 6 through COSO Principle 9, the organization must specify suitable objectives, identify and analyze risks to those objectives, assess fraud risk, and identify significant changes that could affect internal control. In a codebase context, this translates to documented security objectives, formal risk identification processes, fraud risk mitigation, and change management practices.

### COSO Principle 6 — Specifies Suitable Objectives

COSO Principle 6 requires the organization to specify objectives with sufficient clarity to enable the identification and assessment of risks relating to objectives. For a codebase, this means documented business objectives, security objectives, and control objectives.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-BUSINESS-LOGIC` | Partial | No | Deficient | CRM business objectives implemented in code (Sales, Marketing, Support, Communication modules) but never formally specified. |
| `SYS-SECURITY` | No | No | Deficient | No documented security objectives. Security mechanisms exist but without stated security policy or objectives. |
| `SYS-CONFIG` | No | No | Deficient | No documented configuration management objectives. |
| All other systems | No | No | Deficient | No formal objectives specified for any system. |

**Summary Assessment: Deficient** (Risk Severity: Critical)

No formal objectives — business, security, or control — are documented anywhere in the SplendidCRM codebase per COSO Principle 6. While business objectives are implicitly evident in the code structure (40 CRM module folders implementing Sales, Marketing, Support, Communication, and Activity management), they are never formally specified in any documentation artifact. Security objectives are entirely absent — the codebase implements security mechanisms (authentication, authorization, encryption) without any documented security policy or target security posture. This absence of specified objectives means that risk identification (COSO Principle 7) has no baseline against which to assess threats.

---

### COSO Principle 7 — Identifies and Analyzes Risk

COSO Principle 7 requires the organization to identify risks to the achievement of its objectives across the entity and to analyze risks as a basis for determining how the risks should be managed. For a codebase, this includes threat modeling, security risk assessments, and architectural risk documentation.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | No | No | Not Implemented | MD5 password hashing (Source: `SplendidCRM/_code/Security.cs:L393-406`) and disabled request validation (Source: `SplendidCRM/Web.config:L51`) represent known risks with no documented risk acceptance. |
| `SYS-IIS-CFG` | No | No | Not Implemented | `customErrors mode="Off"` (Source: `SplendidCRM/Web.config:L51`), `requestValidationMode="2.0"` — security settings weakened without documented risk analysis. |
| `SYS-CONFIG` | No | No | Not Implemented | Connection string with plaintext credentials in `Web.config` (Source: `SplendidCRM/Web.config:L5`) — no risk assessment documented. |
| All other systems | No | No | Not Implemented | No formal risk assessment, threat modeling, or security architecture documentation exists for any system. |

**Summary Assessment: Not Implemented** (Risk Severity: Critical)

COSO Principle 7 is not implemented across the entire SplendidCRM codebase. No formal risk assessment documentation, threat modeling artifacts, security architecture documentation, or risk acceptance records exist. Known high-risk configurations — including MD5 password hashing for backward compatibility with SugarCRM (Source: `SplendidCRM/_code/Security.cs:L393`), disabled ASP.NET request validation (`requestValidationMode="2.0"`, `enableEventValidation="false"`, `validateRequest="false"`), and `customErrors mode="Off"` exposing detailed error messages (Source: `SplendidCRM/Web.config:L51`) — exist without any documented risk acceptance, compensating control analysis, or mitigation plan. The absence of COSO Principle 7 means the organization cannot systematically determine how risks should be managed.

---

### COSO Principle 8 — Assesses Fraud Risk

COSO Principle 8 requires the organization to consider the potential for fraud in assessing risks to the achievement of objectives. In a codebase context, this includes anti-fraud controls such as audit logging, privilege escalation prevention, impersonation safeguards, and abuse detection.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-AUDIT` | Yes | Partial | Deficient | SQL audit triggers provide after-the-fact fraud detection via entity change logging (Source: `SQL Scripts Community/Triggers/BuildAllAuditTables.1.sql`). No proactive fraud detection. |
| `SYS-SECURITY` | Yes | Partial | Deficient | `IS_ADMIN` enforcement prevents basic privilege escalation (Source: `SplendidCRM/_code/Security.cs:L137-151`). No separation-of-duties enforcement. |
| `SYS-API-ADMIN` | Yes | Partial | Deficient | User impersonation capability exists (Source: `SplendidCRM/Administration/Impersonation.svc.cs`) — potential abuse vector. Admin-only access control present but no dual-approval mechanism. |
| `SYS-SQL-DB` | Yes | Partial | Deficient | `USERS_LOGINS` table tracks login events. Audit tables track data changes. No anomaly detection. |
| All other systems | Partial | No | Deficient | Basic audit controls exist systemwide via `SYS-AUDIT` and `SYS-ERROR-OBSERVABILITY` but no explicit fraud risk assessment or proactive fraud mitigation strategy. |

**Summary Assessment: Deficient** (Risk Severity: Moderate)

COSO Principle 8 is deficient across the codebase. Anti-fraud controls exist in limited form: `SYS-AUDIT` provides comprehensive entity-level audit trails, `SYS-SQL-DB` tracks login events via `USERS_LOGINS`, and `SYS-SECURITY` enforces role-based access to prevent basic privilege escalation. However, no explicit fraud risk assessment has been conducted. The user impersonation capability in `SYS-API-ADMIN` (Source: `SplendidCRM/Administration/Impersonation.svc.cs`) represents a significant fraud risk vector — while admin-only access is enforced, no dual-approval mechanism, time-limited sessions, or enhanced audit logging specific to impersonation actions is observed. No anomaly detection, behavioral analysis, or proactive fraud monitoring exists.

---

### COSO Principle 9 — Identifies and Analyzes Significant Change

COSO Principle 9 requires the organization to identify and assess changes that could significantly impact the system of internal control. For a codebase, this means change management for dependencies, code modifications, configuration changes, and technology evolution.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-DEPENDENCY-MGMT` | No | No | Not Implemented | 38 manually managed DLLs in `BackupBin2012/` with no automated change detection, no NuGet package management, no SBOM (Source: `SplendidCRM/SplendidCRM7_VS2017.csproj`). |
| `SYS-BUILD-PIPELINE` | No | No | Not Implemented | No CI/CD pipeline to detect or analyze code changes. `Build.bat` rebuilds SQL from scratch with no incremental change tracking (Source: `SQL Scripts Community/Build.bat`). |
| `SYS-INTEGRATION-STUBS` | No | No | Not Implemented | 16 `Spring.Social.*` enterprise stubs represent obsolete change without cleanup — dead code persists without change impact analysis. |
| `SYS-REACT-SPA` | Partial | No | Not Implemented | `package.json` tracks npm dependency versions but no automated vulnerability scanning or change impact analysis (Source: `SplendidCRM/React/package.json`). |
| `SYS-ANGULAR-CLIENT` | Partial | No | Not Implemented | `package.json` exists but Angular ~13.3.0 is unmaintained — no change impact analysis for the experimental client. |
| All other systems | No | No | Not Implemented | No formal change identification or analysis process exists for any system. |

**Summary Assessment: Not Implemented** (Risk Severity: Critical)

COSO Principle 9 is not implemented across the SplendidCRM codebase. No formal change management process exists for any system. The most significant evidence is `SYS-DEPENDENCY-MGMT`: 38 .NET DLLs are manually managed in `BackupBin2012/` without NuGet, SBOM, or any automated change detection mechanism. Dependencies can change without triggering any review or impact analysis. The persistence of 16 non-functional `Spring.Social.*` integration stubs in `SYS-INTEGRATION-STUBS` demonstrates that significant changes (removal of enterprise functionality) occurred without corresponding cleanup or change impact documentation. The absence of CI/CD in `SYS-BUILD-PIPELINE` means no automated gates exist to detect or analyze any code change across the entire codebase.

---

## Component 3 — Control Activities

The Control Activities component of the COSO framework encompasses the actions established through policies and procedures that help ensure that management's directives to mitigate risks are carried out. Per COSO Principle 10 through COSO Principle 12, these are the technical controls implemented in code, the general technology controls that govern the IT environment, and the deployment of controls through policies and procedures. In a codebase context, Control Activities represent the strongest component for SplendidCRM — the area where code-level controls demonstrate the most robust implementation.

### COSO Principle 10 — Selects and Develops Control Activities

COSO Principle 10 requires the organization to select and develop control activities that contribute to the mitigation of risks to the achievement of objectives to acceptable levels. This is the most critical COSO principle for the SplendidCRM audit — it directly maps to Directive 1 (Structural Integrity) and encompasses the technical security controls implemented throughout the codebase.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | Yes | Partial | Partially Effective | 4-tier authorization (module ACL, team filtering, field-level, record-level). `Security.Filter()` injects parameterized SQL for record-level access (Source: `SplendidCRM/_code/Security.cs:L842-856`). MD5 hashing weakens authentication control (Source: `SplendidCRM/_code/Security.cs:L393-406`). |
| `SYS-AUTH-AD` | Yes | Partial | Partially Effective | Windows/NTLM integration present. Some SSO methods partially stubbed in Community Edition. |
| `SYS-AUTH-DUO` | Yes | Yes | Effective | 2FA challenge/callback flow provides strong secondary authentication control. |
| `SYS-API-REST` | Yes | Partial | Partially Effective | All endpoints enforce `Security.IsAuthenticated()` and module ACL. Input validation gaps due to disabled request validation in `SYS-IIS-CFG`. |
| `SYS-API-SOAP` | Yes | Partial | Partially Effective | Session-based authentication enforced. Legacy SOAP protocol surface area increases risk. |
| `SYS-API-ADMIN` | Yes | Yes | Partially Effective | `IS_ADMIN` enforcement on all admin endpoints. Impersonation control present. |
| `SYS-CACHE` | Yes | Yes | Partially Effective | Centralized metadata management prevents inconsistent cache state. Cache invalidation tied to scheduler events. |
| `SYS-INIT` | Yes | Yes | Effective | TLS 1.2 enforcement in `Application_Start` (Source: `SplendidCRM/Global.asax.cs:L97-100`). SameSite cookie hardening in `Session_Start` (Source: `SplendidCRM/Global.asax.cs:L151-175`). Unobserved task exception handler (Source: `SplendidCRM/Global.asax.cs:L96`). |
| `SYS-DB-ACCESS` | Yes | Yes | Partially Effective | Parameterized queries via `Sql.cs` prevent SQL injection. Provider-agnostic factory pattern. Transaction management per-operation. |
| `SYS-ERROR-OBSERVABILITY` | Yes | Partial | Partially Effective | Centralized error logging to `SYSTEM_LOG`. ThreadAbortException filtering (Source: `SplendidCRM/_code/SplendidError.cs:L49,59`). No external alerting. |
| `SYS-BUSINESS-LOGIC` | Yes | Partial | Partially Effective | ACL enforcement via `Security.Filter()` on all CRUD operations. Metadata-driven layout rendering. |
| `SYS-IMPORT-EXPORT` | Yes | Partial | Partially Effective | Import pipeline includes duplicate detection. ACL-aware field filtering on export. |
| `SYS-CAMPAIGN` | Yes | Partial | Partially Effective | Campaign tracker endpoints are publicly accessible without authentication — design decision for email tracking but increases attack surface. |
| `SYS-EMAIL` | Yes | Partial | Partially Effective | Multi-provider mail abstraction. SMTP/IMAP/POP3 protocol handling. |
| `SYS-SMS-TELEPHONY` | Yes | Partial | Partially Effective | TwiML webhook processing. Phone number normalization. |
| `SYS-REALTIME` | Yes | Partial | Partially Effective | `SplendidHubAuthorize.cs` implements `IAuthorizeHubConnection` and `IAuthorizeHubMethodInvocation`. Session-cookie coupling creates bypass risk when sessions expire (Source: `SplendidCRM/_code/SignalR/SplendidHubAuthorize.cs:L34-39`). |
| `SYS-SCHEDULER` | Yes | Yes | Partially Effective | Reentrancy guards via `bInsideTimer` flags prevent concurrent execution (Source: `SplendidCRM/_code/SchedulerUtils.cs:L34,657-659`). Job-level logging. |
| `SYS-WORKFLOW` | No | No | Not Implemented | Workflow stubs compiled but non-functional in Community Edition. |
| `SYS-CONFIG` | Yes | Partial | Deficient | `requestValidationMode="2.0"`, `enableEventValidation="false"`, `validateRequest="false"` deliberately weaken security controls (Source: `SplendidCRM/Web.config`). |
| `SYS-ADMIN` | Yes | Yes | Partially Effective | IS_ADMIN enforcement across 45 admin sub-modules. |
| `SYS-REPORTING` | Yes | Partial | Partially Effective | RDL/RDS sanitization present. Dynamic SQL construction in reports increases risk. |
| `SYS-CONTENT` | Yes | Partial | Partially Effective | Document version tracking. ACL-mediated access. |
| `SYS-L10N` | Yes | Yes | Effective | Localization data seeded from controlled SQL scripts. No dynamic security risk. |
| `SYS-INTEGRATION-STUBS` | No | No | Not Implemented | Stubs throw exceptions — no functional control activities. |
| `SYS-SQL-DB` | Yes | Yes | Partially Effective | Stored procedures implement CRUD with parameterized queries. INFORMATION_SCHEMA guards on DDL. Idempotent upgrade paths. |
| `SYS-AUDIT` | Yes | Yes | Effective | Comprehensive audit trigger generation for all CRM entities with user attribution and timestamps. |
| `SYS-ASPNET-APP` | Yes | Partial | Partially Effective | Application host with lifecycle management. Error boundary at application level. |
| `SYS-IIS-CFG` | Yes | Partial | Deficient | Session state configuration present but `customErrors mode="Off"` exposes internal state (Source: `SplendidCRM/Web.config:L51`). Multiple security controls disabled. |
| `SYS-REACT-SPA` | Yes | Partial | Partially Effective | TypeScript type safety. Client-side routing. IndexedDB offline caching. |
| `SYS-ANGULAR-CLIENT` | Partial | No | Deficient | Experimental status — minimal control activities for non-production client. |
| `SYS-HTML5-CLIENT` | Yes | Partial | Deficient | Legacy jQuery 1.4.2–2.2.4 with known vulnerabilities. AES encryption utilities present. |
| `SYS-WEBFORMS` | Yes | Partial | Partially Effective | Server-rendered with ViewState. ACL filtering per request. |
| `SYS-BUILD-PIPELINE` | Partial | No | Deficient | Build scripts exist but no CI/CD, no automated testing, no static analysis. |
| `SYS-DEPENDENCY-MGMT` | No | No | Not Implemented | No automated dependency vulnerability scanning, no SBOM, no version pinning verification. |

**Summary Assessment: Partially Effective** (Risk Severity: Critical)

COSO Principle 10 is the strongest of all 17 principles in the SplendidCRM codebase, yet still only achieves Partially Effective status. Strong controls exist: the 4-tier authorization model in `SYS-SECURITY`, TLS 1.2 enforcement and SameSite cookie hardening in `SYS-INIT`, reentrancy guards in `SYS-SCHEDULER`, comprehensive audit triggers in `SYS-AUDIT`, and parameterized queries in `SYS-DB-ACCESS`. However, these controls are critically undermined by two factors: (1) the deliberate weakening of ASP.NET security controls in `SYS-CONFIG` / `SYS-IIS-CFG` (`requestValidationMode="2.0"`, `enableEventValidation="false"`, `validateRequest="false"`, `customErrors mode="Off"`), and (2) the complete absence of automated testing across all tiers — zero unit tests, zero integration tests, zero E2E tests, no CI/CD — which means control effectiveness cannot be systematically verified per COSO Principle 10.

---

### COSO Principle 11 — Selects and Develops General Controls over Technology

COSO Principle 11 requires the organization to select and develop general control activities over technology to support the achievement of objectives. These are the technology infrastructure controls that provide the platform for other control activities to operate effectively — including centralized services, input validation frameworks, encryption standards, and development tooling.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-DB-ACCESS` | Yes | Yes | Partially Effective | Database access centralized through `Sql.cs` / `SqlBuild.cs` / `SqlClientFactory.cs` — provider-agnostic with parameterized queries (Source: `SplendidCRM/_code/Sql.cs`). |
| `SYS-CACHE` | Yes | Yes | Partially Effective | Cache management centralized through `SplendidCache.cs` — single point of metadata management (Source: `SplendidCRM/_code/SplendidCache.cs`). |
| `SYS-ERROR-OBSERVABILITY` | Yes | Partial | Partially Effective | Error handling centralized through `SplendidError.cs` — consistent logging pattern (Source: `SplendidCRM/_code/SplendidError.cs:L69-74`). No external APM or distributed tracing. |
| `SYS-SECURITY` | Yes | Partial | Partially Effective | Rijndael symmetric encryption for stored secrets (Source: `SplendidCRM/_code/Security.cs:L412-454`). MD5 hashing weakens the overall cryptographic control. |
| `SYS-IIS-CFG` | Yes | Partial | Deficient | `customErrors mode="Off"` may expose internal state to attackers (Source: `SplendidCRM/Web.config:L51`). No input validation framework beyond disabled ASP.NET validation. |
| `SYS-API-REST` | Yes | Partial | Partially Effective | WCF REST service model configured. JSON serialization via `RestUtil.cs`. |
| `SYS-API-SOAP` | Yes | Partial | Partially Effective | SOAP service with runtime WSDL generation. |
| `SYS-REACT-SPA` | Yes | Partial | Partially Effective | TypeScript 5.3.3 provides compile-time type safety. Webpack 5.90.2 for module bundling. |
| `SYS-HTML5-CLIENT` | Yes | Partial | Deficient | jQuery 1.4.2–2.2.4 contains known XSS and security vulnerabilities. |
| `SYS-BUILD-PIPELINE` | Partial | No | Deficient | Build tools exist (MSBuild, Webpack, Build.bat) but no static analysis, no code review tooling, no security scanning. |
| All other systems | Partial | Partial | Partially Effective | Centralization patterns (security, cache, error, database) provide a solid technology control foundation, but tooling gaps undermine effectiveness. |

**Summary Assessment: Partially Effective** (Risk Severity: Moderate)

COSO Principle 11 benefits from strong centralization patterns: `SYS-DB-ACCESS` provides a single database access layer with parameterized queries, `SYS-CACHE` centralizes all metadata caching, and `SYS-ERROR-OBSERVABILITY` provides a unified error logging mechanism. These technology controls are well-designed and consistently used across the codebase. However, effectiveness is undermined by: (1) the `customErrors mode="Off"` configuration in `SYS-IIS-CFG` which may expose internal application state, (2) the absence of any input validation framework beyond the disabled ASP.NET validation, (3) the lack of any static analysis, code review tooling, or automated security scanning in `SYS-BUILD-PIPELINE`, and (4) the presence of legacy jQuery 1.4.2 in `SYS-HTML5-CLIENT` with known security vulnerabilities.

---

### COSO Principle 12 — Deploys through Policies and Procedures

COSO Principle 12 requires the organization to deploy control activities through policies and procedures that put those activities into action. For a codebase, this means documented deployment procedures, configuration management, and operational runbooks.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-BUILD-PIPELINE` | Partial | Partial | Deficient | `Build.bat` provides repeatable SQL deployment. React build via `yarn build`. MSBuild via `.csproj`. No documented deployment procedures beyond build scripts (Source: `SQL Scripts Community/Build.bat`). |
| `SYS-CONFIG` | Partial | No | Deficient | `Web.config` stores configuration but no configuration management for different deployment environments. Plaintext connection string with embedded credentials (Source: `SplendidCRM/Web.config:L5`). |
| `SYS-IIS-CFG` | Partial | No | Deficient | IIS configuration in `Web.config` but no documented deployment or hardening procedures. |
| `SYS-SCHEDULER` | Partial | Partial | Deficient | Timer intervals configured as constants in `Global.asax.cs` — repeatable but no externalized configuration management. |
| `SYS-SQL-DB` | Partial | Partial | Deficient | `Build.bat` concatenates SQL scripts into a single deployment artifact. Idempotent schema scripts. No migration tooling. |
| All other systems | No | No | Deficient | No documented deployment policies, no approval workflows, no change management procedures. |

**Summary Assessment: Deficient** (Risk Severity: Moderate)

COSO Principle 12 is deficient across the codebase. Build procedures exist — `Build.bat` provides a repeatable SQL deployment pipeline, `yarn build` compiles the React SPA, and MSBuild compiles the .NET application — but these are build mechanisms, not deployment policies. No documented deployment procedures exist for any environment (development, staging, production). No configuration management strategy differentiates between environments — `SYS-CONFIG` stores a connection string with plaintext credentials directly in `Web.config`. No deployment approval workflows, no rollback procedures, no operational runbooks, and no disaster recovery documentation are present. Per COSO Principle 12, the build tooling exists but the policies and procedures to govern its use are absent.

---

## Component 4 — Information & Communication

The Information & Communication component of the COSO framework addresses the need for quality information to support internal control functioning, and for effective internal and external communication. Per COSO Principle 13 through COSO Principle 15, the organization must obtain, generate, and use relevant quality information; communicate internally the information necessary for internal control to function; and communicate with external parties regarding matters affecting internal control. In a codebase context, this is the weakest COSO component for SplendidCRM, characterized by near-zero documentation infrastructure.

### COSO Principle 13 — Uses Relevant Information

COSO Principle 13 requires the organization to obtain or generate and use relevant, quality information to support the functioning of internal control. For a codebase, this translates to the availability and quality of data access layers, metadata infrastructure, configuration management, and information retrieval mechanisms.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-CACHE` | Yes | Yes | Partially Effective | Thousands of metadata query getters providing rich information infrastructure for all systems (Source: `SplendidCRM/_code/SplendidCache.cs`). Data quality depends on cache invalidation correctness. |
| `SYS-SQL-DB` | Yes | Yes | Partially Effective | 581 view projections provide comprehensive data access layers across list, detail, edit, sync, SOAP, relationship, and metadata formats. |
| `SYS-DB-ACCESS` | Yes | Yes | Partially Effective | Provider-agnostic database operations with parameterized queries ensure data integrity in retrieval. |
| `SYS-CONFIG` | Yes | Partial | Partially Effective | Configuration stored in `CONFIG` SQL table and `Web.config` — accessible but poorly documented. |
| `SYS-API-REST` | Yes | Partial | Partially Effective | `Application_GetReactState` provides bulk metadata transfer to clients. No API documentation for consumers. |
| `SYS-REPORTING` | Yes | Partial | Partially Effective | RDL-based reporting with dataset rehydration. Information accessible but report definitions undocumented. |
| All other systems | Yes | Partial | Partially Effective | Rich information infrastructure exists throughout the codebase but is poorly documented and lacks external accessibility guidance. |

**Summary Assessment: Partially Effective** (Risk Severity: Moderate)

COSO Principle 13 is partially effective due to strong technical implementation paired with documentation gaps. The information infrastructure is robust: `SYS-CACHE` provides thousands of metadata query getters consumed by virtually every system, `SYS-SQL-DB` offers 581 view projections in multiple formats, and `SYS-DB-ACCESS` ensures data integrity through parameterized queries. However, information quality is undermined by: (1) poor documentation of data schemas and business rules, (2) no data dictionary or metadata documentation for the CONFIG table, and (3) no API documentation describing the information available through `SYS-API-REST` or `SYS-API-SOAP`. Per COSO Principle 13, the organization generates and stores relevant information effectively but fails to make it accessible through documentation.

---

### COSO Principle 14 — Communicates Internally

COSO Principle 14 requires the organization to internally communicate information, including objectives and responsibilities for internal control, necessary to support the functioning of internal control. This is the most deficient COSO principle in the SplendidCRM codebase, directly mapping to Directive 5 (Documentation Coverage).

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SECURITY` | No | No | Not Implemented | Zero XML doc comments on any public method. No API reference documentation. No security architecture documentation. |
| `SYS-CACHE` | No | No | Not Implemented | Zero XML doc comments. No documentation of cache invalidation patterns, data flow, or usage guidelines. |
| `SYS-DB-ACCESS` | No | No | Not Implemented | Zero XML doc comments. No documentation of database access patterns or transaction management approach. |
| `SYS-ERROR-OBSERVABILITY` | No | No | Not Implemented | Zero XML doc comments. No documentation of error handling categories or logging conventions. |
| `SYS-API-REST` | No | No | Not Implemented | No Swagger/OpenAPI specification. No API reference documentation. No endpoint documentation (Source: `SplendidCRM/Rest.svc.cs`). |
| `SYS-API-SOAP` | No | No | Not Implemented | No maintained WSDL documentation — WSDL is runtime-generated only (Source: `SplendidCRM/soap.asmx.cs`). |
| `SYS-API-ADMIN` | No | No | Not Implemented | No documentation for admin API endpoints. |
| `SYS-SQL-DB` | No | No | Not Implemented | SQL script headers contain only AGPLv3 license text — zero functional documentation across 833 procedures and 581 views. |
| `SYS-INIT` | No | No | Not Implemented | No bootstrap sequence documentation. No startup dependency documentation. |
| `SYS-L10N` | Partial | Partial | Deficient | Terminology seed scripts provide structured localization data but no usage documentation. |
| `SYS-BUILD-PIPELINE` | Partial | Partial | Deficient | `README.md` (78 lines) covers build prerequisites and DLL dependencies but lacks architecture overview, security guidance, or operational documentation. |
| All other systems | No | No | Not Implemented | Zero XML doc comments, zero architecture decision records, zero API documentation, zero developer onboarding documentation, zero `CONTRIBUTING.md`. |

**Summary Assessment: Not Implemented** (Risk Severity: Critical)

COSO Principle 14 is the weakest principle in the entire SplendidCRM codebase. The `README.md` (78 lines) is the sole documentation artifact in the entire repository. No XML doc comments exist on any public API across 74+ C# utility classes in `_code/`. No Swagger/OpenAPI specification documents the REST API. No architecture decision records explain design choices. No `CONTRIBUTING.md`, no developer onboarding guide, no security documentation, no operational runbooks. SQL script headers contain only AGPLv3 license text — zero functional documentation across 833 stored procedures. Per COSO Principle 14, internal communication infrastructure for code understanding is effectively non-existent. This constitutes a Critical-severity gap because it prevents effective knowledge transfer, impedes new developer onboarding, and makes internal control objectives impossible to communicate.

---

### COSO Principle 15 — Communicates Externally

COSO Principle 15 requires the organization to communicate with external parties regarding matters affecting the functioning of internal control. For a codebase, this translates to external API documentation, changelogs, release notes, and external interface specifications.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-API-SOAP` | Partial | Partial | Deficient | SugarCRM-compatible SOAP interface with runtime-generated WSDL. No maintained external documentation. |
| `SYS-API-REST` | Partial | No | Deficient | REST API exists but no external API documentation (no Swagger, no reference docs). |
| `SYS-REPORTING` | Partial | Partial | Deficient | Report engine generates output for external consumption but no documentation of report formats or APIs. |
| `SYS-CAMPAIGN` | Partial | Partial | Deficient | Campaign tracker endpoints (`campaign_trackerv2.aspx.cs`, `image.aspx.cs`) are publicly accessible external interfaces with no documentation. |
| `SYS-BUILD-PIPELINE` | Partial | Partial | Deficient | No changelog or formal release notes beyond `Versions.xml`. |
| All other systems | No | No | Deficient | No external communication artifacts exist for any system. |

**Summary Assessment: Deficient** (Risk Severity: Moderate)

COSO Principle 15 is deficient across the codebase. External interfaces exist — `SYS-API-SOAP` provides a SugarCRM-compatible SOAP API, `SYS-API-REST` provides the SPA client interface, and `SYS-CAMPAIGN` exposes public campaign tracking endpoints — but none are documented for external consumers. The SOAP API's WSDL is runtime-generated only and not maintained as a documentation artifact. No external changelog, release notes (beyond the structured `Versions.xml`), API reference, or integration guide exists. Per COSO Principle 15, external communication mechanisms exist at the technical level but are undocumented and therefore ineffective for supporting external parties' understanding of the system.

---

## Component 5 — Monitoring Activities

The Monitoring Activities component of the COSO framework assesses whether each of the five components of internal control, and relevant principles, is present and functioning over time. Per COSO Principle 16 and COSO Principle 17, the organization must conduct ongoing and/or separate evaluations of internal control components and evaluate and communicate deficiencies in a timely manner. In a codebase context, this translates to automated monitoring, quality evaluation processes, and deficiency reporting mechanisms.

### COSO Principle 16 — Conducts Ongoing and/or Separate Evaluations

COSO Principle 16 requires the organization to select, develop, and perform ongoing and/or separate evaluations to ascertain whether the components of internal control are present and functioning. This principle directly maps to Directive 6 (Accuracy Validation). Notably, this audit itself constitutes a "Separate Evaluation" per COSO Principle 16.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-SCHEDULER` | Yes | Partial | Deficient | `SchedulerUtils.OnTimer` monitors system events via `vwSYSTEM_EVENTS` (Source: `SplendidCRM/_code/SchedulerUtils.cs:L653`). Operational monitoring only — no security or quality evaluation. |
| `SYS-AUDIT` | Yes | Yes | Deficient | SQL audit triggers provide continuous entity-level monitoring. Effective for data change detection but not for control effectiveness evaluation. |
| `SYS-ERROR-OBSERVABILITY` | Yes | Partial | Deficient | `SplendidError.cs` provides continuous error logging. Admin UI (`SYS-ADMIN`) provides `SystemLog` viewing. No automated evaluation or alerting. |
| `SYS-ASPNET-APP` | Yes | Partial | Deficient | `SystemCheck.aspx.cs` provides manual diagnostics endpoint for one-time system checks (Source: `SplendidCRM/SystemCheck.aspx.cs`). |
| `SYS-SQL-DB` | Yes | Partial | Deficient | `USERS_LOGINS` tracking provides ongoing login monitoring. No automated analysis of login patterns. |
| `SYS-BUILD-PIPELINE` | No | No | Not Implemented | No CI/CD pipeline, no automated testing, no static analysis — zero automated quality evaluation. |
| `SYS-DEPENDENCY-MGMT` | No | No | Not Implemented | No automated dependency vulnerability scanning. No periodic dependency evaluation. |
| All other systems | No | No | Deficient | No automated security evaluation, no penetration testing history, no code quality evaluation tooling across any system. |

**Summary Assessment: Deficient** (Risk Severity: Critical)

COSO Principle 16 is deficient across the codebase. Basic operational monitoring exists: `SYS-SCHEDULER` monitors system events, `SYS-AUDIT` provides continuous entity-level audit trails, `SYS-ERROR-OBSERVABILITY` logs errors, and `SystemCheck.aspx.cs` provides a manual diagnostics endpoint. However, no systematic evaluation of internal control effectiveness exists: no automated security evaluations, no penetration testing history, no code quality evaluation tooling, no static analysis, and — most critically — zero automated tests of any kind across all tiers. The complete absence of testing infrastructure means there is no mechanism to conduct ongoing evaluations of whether control activities (COSO Principle 10) remain effective over time. Per COSO Principle 16, operational monitoring is present but systematic evaluation is absent.

---

### COSO Principle 17 — Evaluates and Communicates Deficiencies

COSO Principle 17 requires the organization to evaluate and communicate internal control deficiencies in a timely manner to those parties responsible for taking corrective action. For a codebase, this translates to error reporting processes, deficiency escalation mechanisms, vulnerability disclosure policies, and corrective action tracking.

| system_id | Present? | Functioning? | Rating | Evidence |
|---|---|---|---|---|
| `SYS-ERROR-OBSERVABILITY` | Yes | Partial | Deficient | `SplendidError.cs` centralizes runtime error reporting to `SYSTEM_LOG` with source attribution (Source: `SplendidCRM/_code/SplendidError.cs:L36-44`). No escalation mechanism beyond database logging. |
| `SYS-ADMIN` | Yes | Partial | Deficient | Admin UI provides `SystemLog` viewing capability, enabling administrators to review deficiencies. No automated alerting. |
| `SYS-AUDIT` | Yes | Partial | Deficient | Audit triggers detect data integrity changes but do not evaluate or communicate deficiencies. |
| `SYS-SCHEDULER` | Yes | Partial | Deficient | Job-level logging to `SYSTEM_LOG` reports scheduler deficiencies. No automated escalation. |
| All other systems | No | No | Deficient | No formal deficiency reporting process, no escalation procedures, no vulnerability disclosure policy (`SECURITY.md`), no corrective action tracking across any system. |

**Summary Assessment: Deficient** (Risk Severity: Moderate)

COSO Principle 17 is deficient across the codebase. The primary deficiency communication mechanism is `SYS-ERROR-OBSERVABILITY`: `SplendidError.cs` logs errors to `SYSTEM_LOG` with categorized error types (Warning, Error) and source file attribution. Administrators can review deficiencies via the `SystemLog` admin module. However, this mechanism is purely reactive and passive — errors must be manually discovered through the admin UI. No automated alerting exists, no escalation procedures are defined, no vulnerability disclosure policy (`SECURITY.md`) exists, and no corrective action tracking mechanism is present. Per COSO Principle 17, error detection exists but systematic deficiency evaluation and timely communication to responsible parties is absent.

---

## Consolidated COSO Assessment Matrix

The following matrix provides a consolidated view of all 17 COSO principles with their assessment ratings, key gaps, and associated risk severity.

| COSO Component | Principle # | Principle Name | Rating | Risk Severity | Key Gap |
|---|---|---|---|---|---|
| Control Environment | COSO Principle 1 | Integrity and Ethical Values | Partially Effective | Moderate | AGPLv3 licensing present; no code of conduct, contributor guidelines, or security disclosure policy |
| Control Environment | COSO Principle 2 | Oversight Responsibility | Deficient | Critical | No code review process, no pull request workflow, no approval gates |
| Control Environment | COSO Principle 3 | Structure, Authority, and Responsibility | Partially Effective | Moderate | Application-level structure present; organizational code governance absent |
| Control Environment | COSO Principle 4 | Competence | Partially Effective | Moderate | Competent implementation evident; competence framework undocumented |
| Control Environment | COSO Principle 5 | Accountability | Partially Effective | Moderate | Application-level audit trails present; development process accountability absent |
| Risk Assessment | COSO Principle 6 | Suitable Objectives | Deficient | Critical | Business objectives implicit in code; security and control objectives unspecified |
| Risk Assessment | COSO Principle 7 | Risk Identification and Analysis | Not Implemented | Critical | No threat modeling, no risk assessment documentation, known risks without documented acceptance |
| Risk Assessment | COSO Principle 8 | Fraud Risk | Deficient | Moderate | Basic audit controls; no explicit fraud risk assessment; impersonation vector |
| Risk Assessment | COSO Principle 9 | Significant Change | Not Implemented | Critical | No CI/CD, no automated change detection, 38 manually managed DLLs, obsolete stubs |
| Control Activities | COSO Principle 10 | Control Activities | Partially Effective | Critical | Strong 4-tier authorization and TLS; undermined by disabled validation and zero testing |
| Control Activities | COSO Principle 11 | Technology Controls | Partially Effective | Moderate | Good centralization patterns; undermined by configuration weaknesses and absent tooling |
| Control Activities | COSO Principle 12 | Policies and Procedures | Deficient | Moderate | Build scripts exist; deployment policies, procedures, and runbooks absent |
| Information & Communication | COSO Principle 13 | Relevant Information | Partially Effective | Moderate | Rich data infrastructure; poorly documented for human consumption |
| Information & Communication | COSO Principle 14 | Internal Communication | Not Implemented | Critical | README.md (78 lines) is sole documentation; zero API docs, zero XML doc comments |
| Information & Communication | COSO Principle 15 | External Communication | Deficient | Moderate | External interfaces exist; all undocumented |
| Monitoring Activities | COSO Principle 16 | Ongoing/Separate Evaluations | Deficient | Critical | Basic operational monitoring; no systematic quality or security evaluations; zero automated testing |
| Monitoring Activities | COSO Principle 17 | Deficiency Communication | Deficient | Moderate | Error logging present; no automated alerting, escalation, or vulnerability disclosure |

### Assessment Distribution

| Rating | Count | Percentage | Principles |
|---|---|---|---|
| **Effective** | 0 | 0% | — |
| **Partially Effective** | 7 | 41% | COSO Principle 1, 3, 4, 5, 10, 11, 13 |
| **Deficient** | 7 | 41% | COSO Principle 2, 6, 8, 12, 15, 16, 17 |
| **Not Implemented** | 3 | 18% | COSO Principle 7, 9, 14 |

**Critical Finding:** No COSO principle achieves an **Effective** rating. The highest-rated principles are only Partially Effective, and 10 out of 17 principles (59%) are rated Deficient or Not Implemented.

---

## COSO-to-System Mapping Matrix

The following matrix maps each `system_id` from the [system registry](./system-registry.md) to all 17 COSO principles, using the symbols defined below.

**Legend:**
- ✅ = Effective
- ⚠️ = Partially Effective
- ❌ = Deficient
- ⊘ = Not Implemented
- — = Not Applicable

| system_id | P1 | P2 | P3 | P4 | P5 | P6 | P7 | P8 | P9 | P10 | P11 | P12 | P13 | P14 | P15 | P16 | P17 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `SYS-SECURITY` | ⚠️ | ❌ | ⚠️ | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | ❌ | ❌ | ❌ |
| `SYS-AUTH-AD` | ⚠️ | ❌ | ⚠️ | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-AUTH-DUO` | ⚠️ | ❌ | — | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ✅ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-API-REST` | ⚠️ | ❌ | — | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | ❌ | ❌ | ❌ |
| `SYS-API-SOAP` | ⚠️ | ❌ | — | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | ❌ | ❌ | ❌ |
| `SYS-API-ADMIN` | ⚠️ | ❌ | ⚠️ | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-CACHE` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-INIT` | ⚠️ | ❌ | ⚠️ | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ✅ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-DB-ACCESS` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-ERROR-OBSERVABILITY` | ⚠️ | ❌ | — | ⚠️ | ⚠️ | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-BUSINESS-LOGIC` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-IMPORT-EXPORT` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-CAMPAIGN` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-EMAIL` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-SMS-TELEPHONY` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-REALTIME` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-SCHEDULER` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-WORKFLOW` | ⚠️ | ❌ | — | — | — | ❌ | ⊘ | — | ⊘ | ⊘ | — | ❌ | — | ⊘ | — | — | — |
| `SYS-CONFIG` | ⚠️ | ❌ | ⚠️ | — | — | ❌ | ⊘ | — | ⊘ | ❌ | ❌ | ❌ | ⚠️ | ⊘ | — | — | — |
| `SYS-ADMIN` | ⚠️ | ❌ | ⚠️ | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-REPORTING` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | ❌ | ❌ | ❌ |
| `SYS-CONTENT` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-L10N` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ✅ | — | ❌ | — | ❌ | — | — | — |
| `SYS-INTEGRATION-STUBS` | ⚠️ | ❌ | — | — | — | ❌ | ⊘ | — | ⊘ | ⊘ | — | ❌ | — | ⊘ | — | — | — |
| `SYS-SQL-DB` | ⚠️ | ❌ | — | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | ⚠️ | ⊘ | — | ❌ | ❌ |
| `SYS-AUDIT` | ⚠️ | ❌ | — | ⚠️ | ⚠️ | ❌ | ⊘ | ❌ | ⊘ | ✅ | — | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-ASPNET-APP` | ⚠️ | ❌ | ⚠️ | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-IIS-CFG` | — | ❌ | ⚠️ | — | — | ❌ | ⊘ | — | ⊘ | ❌ | ❌ | ❌ | — | ⊘ | — | — | — |
| `SYS-REACT-SPA` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | — | — |
| `SYS-ANGULAR-CLIENT` | ⚠️ | ❌ | — | — | — | ❌ | ⊘ | — | ⊘ | ❌ | — | ❌ | — | ⊘ | — | — | — |
| `SYS-HTML5-CLIENT` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ❌ | ❌ | ❌ | — | ⊘ | — | — | — |
| `SYS-WEBFORMS` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | ❌ | ⊘ | ⚠️ | ⚠️ | ❌ | — | ⊘ | — | ❌ | ❌ |
| `SYS-BUILD-PIPELINE` | ⚠️ | ❌ | — | ⚠️ | — | ❌ | ⊘ | — | ⊘ | ❌ | ❌ | ❌ | — | ⊘ | — | ⊘ | — |
| `SYS-DEPENDENCY-MGMT` | ⚠️ | ❌ | — | — | — | ❌ | ⊘ | — | ⊘ | ⊘ | — | ❌ | — | ⊘ | — | ⊘ | — |

---

## Overall Internal Controls Effectiveness

Based on the comprehensive assessment of all 17 COSO principles across all 34 registered systems in SplendidCRM Community Edition v15.2, the overall effectiveness of internal controls over the codebase is assessed as **Partially Effective with Significant Deficiencies**. This determination is made per the COSO 2013 Internal Control — Integrated Framework requirement that all 17 principles must be present and functioning for internal controls to be deemed effective. With zero principles achieving an Effective rating, seven rated Partially Effective, seven rated Deficient, and three rated Not Implemented, the conditions for overall effectiveness are not met.

The **Control Activities** component (COSO Principle 10 through COSO Principle 12) represents the strongest area of the codebase's internal control posture. COSO Principle 10 demonstrates genuinely robust technical controls: the 4-tier authorization model in `SYS-SECURITY`, TLS 1.2 enforcement and SameSite cookie hardening in `SYS-INIT`, parameterized queries in `SYS-DB-ACCESS`, reentrancy guards in `SYS-SCHEDULER`, and comprehensive audit triggers in `SYS-AUDIT`. COSO Principle 11 benefits from strong centralization patterns across the core infrastructure layer (cache, database, error handling). However, even this strongest component is undermined by deliberately weakened ASP.NET security configurations in `SYS-CONFIG` / `SYS-IIS-CFG` and the complete absence of automated testing to verify control effectiveness.

The **Information & Communication** component (COSO Principle 13 through COSO Principle 15) represents the weakest area, with COSO Principle 14 (Internal Communication) rated as Not Implemented. The codebase contains a single 78-line `README.md` as its sole documentation artifact — zero XML doc comments, zero API documentation, zero architecture decision records, and zero developer onboarding materials. This near-total absence of internal communication infrastructure means that control objectives, security requirements, and operational procedures cannot be communicated to developers, operators, or auditors. The **Monitoring Activities** component (COSO Principle 16 through COSO Principle 17) suffers from structural gaps — operational monitoring exists (audit triggers, error logging, system event tracking) but no systematic security evaluations, quality assessments, or automated testing exists to detect control deficiencies over time.

The three most critical gaps requiring immediate attention per the COSO framework are: (1) the **Risk Assessment** component, where COSO Principle 7 (Risk Identification) and COSO Principle 9 (Significant Change) are Not Implemented, meaning the organization has no formal basis for identifying or managing risks to the codebase; (2) the **Information & Communication** component, where COSO Principle 14 (Internal Communication) is Not Implemented, preventing any systematic transfer of control-related knowledge; and (3) the absence of **automated testing** across all systems, which cuts across COSO Principle 10 (Control Activities verification), COSO Principle 16 (Ongoing Evaluations), and COSO Principle 9 (Change Impact Analysis), representing a foundational gap in the ability to maintain effective internal controls over time.

---

*This document was produced as part of the SplendidCRM Community Edition v15.2 Codebase Audit under Directive 0 — COSO Internal Control Framework Mapping. It serves as the governance anchor for all subsequent audit directives (1–8). For the system registry, see [system-registry.md](./system-registry.md). For NIST control mappings, see [nist-mapping.md](./nist-mapping.md). For CIS Controls v8 mappings, see [cis-mapping.md](./cis-mapping.md).*
