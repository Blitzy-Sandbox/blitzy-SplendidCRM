# Blitzy Project Guide — SplendidCRM v15.2 Codebase Audit Documentation Suite

---

## 1. Executive Summary

### 1.1 Project Overview

This project delivers a comprehensive, multi-framework codebase audit documentation suite for **SplendidCRM Community Edition v15.2**, a 20-year-old ASP.NET 4.8 / React 18.2.0 / SQL Server monolithic CRM application licensed under AGPLv3. The audit produces 20 new documentation artifacts organized into 9 sequential Directives (0–8) covering system registry and classification, structural integrity scanning, materiality classification, code quality auditing, cross-cutting dependency analysis, documentation coverage assessment, accuracy validation, operational artifact generation, and a Reveal.js risk executive presentation. All output is assess-only — no SplendidCRM source code was created, modified, or remediated. The documentation suite is mapped to four governance frameworks: COSO 2013 Internal Control Framework (17 Principles), NIST SP 800-53 Rev 5, NIST Cybersecurity Framework, and CIS Controls v8 (IG2/IG3).

### 1.2 Completion Status

```mermaid
pie title Project Completion Status
    "Completed (AI)" : 135
    "Remaining" : 23
```

| Metric | Value |
|---|---|
| **Total Project Hours** | 158 |
| **Completed Hours (AI)** | 135 |
| **Remaining Hours** | 23 |
| **Completion Percentage** | 85.4% |

**Calculation:** 135 completed hours / (135 completed + 23 remaining) = 135 / 158 = **85.4% complete**

### 1.3 Key Accomplishments

- ✅ All 20 AAP-specified documentation files created and validated (11,116 total lines, ~939 KB)
- ✅ 34 systems decomposed across 13 functional domains × 9 architectural layers with Static/Dynamic classification
- ✅ Complete COSO Principles 1–17, NIST SP 800-53 Rev 5, NIST CSF, and CIS Controls v8 mappings per system
- ✅ 25 structural integrity findings documented with NIST/CIS control attribution
- ✅ ~222 components classified as Material or Non-Material
- ✅ 6 code quality audit sub-reports covering security, API, infrastructure, background processing, and database domains
- ✅ Cross-cutting dependency analysis with Blast Radius Scores (Low/Medium/High) for all shared utilities
- ✅ 282 accuracy validation samples achieving 100% accuracy — PASS status per COSO Principle 16
- ✅ 9 GATE: PASS/FAIL checkpoints in the Developer Contribution Guide aligned to COSO/NIST/CIS
- ✅ Self-contained Reveal.js v5.2.1 presentation with 6 slides using SplendidCRM Seven theme CSS
- ✅ 19 Mermaid diagrams embedded across reports (dependency graphs, NIST CSF swimlanes, bootstrap sequences)
- ✅ 257 internal cross-references validated with 0 broken links
- ✅ 23 Blitzy Agent commits on the working branch, all clean

### 1.4 Critical Unresolved Issues

| Issue | Impact | Owner | ETA |
|---|---|---|---|
| Framework control IDs not verified against official publications | COSO/NIST/CIS control ID accuracy unconfirmed by human expert | Human Reviewer | 1–2 weeks |
| No human peer review of 11,116 lines of audit content | Potential inaccuracies in compliance mappings or code quality findings | Human Auditor | 2–3 weeks |
| Reveal.js presentation untested in all target browsers | Edge cases in Safari/Edge rendering unknown | Human Developer | 1 week |
| No documentation hosting or CI pipeline | Audit suite not yet accessible to stakeholders via web | Human DevOps | 1 week |

### 1.5 Access Issues

No access issues identified. All documentation was generated from static codebase analysis without requiring runtime access, API keys, database connections, or external service credentials. The SplendidCRM repository was fully accessible for read-only analysis.

### 1.6 Recommended Next Steps

1. **[High]** Conduct stakeholder and peer review of all 20 documentation artifacts, prioritizing D0 system registry and D3 code quality reports
2. **[High]** Verify all COSO Principle IDs (1–17), NIST SP 800-53 control family codes, and CIS Controls v8 safeguard numbers against official framework publications
3. **[Medium]** Test the Reveal.js presentation (`docs/directive-8-presentation/risk-executive-presentation.html`) across Chrome, Firefox, Safari, and Edge; validate PDF export capability
4. **[Medium]** Set up documentation hosting (GitHub Pages, GitLab Pages, or internal wiki) and configure CI pipeline for Markdown linting and link validation
5. **[Low]** Export 19 embedded Mermaid diagrams to static SVG/PNG files for offline distribution and print-ready deliverables

---

## 2. Project Hours Breakdown

### 2.1 Completed Work Detail

| Component | Hours | Description |
|---|---|---|
| Directive 0 — System Registry | 28 | 4 files (2,547 lines): 34 systems decomposed across 13 functional domains × 9 architectural layers; Static/Dynamic classification matrix; COSO Principles 1–17 mapping per system_id; NIST SP 800-53 Rev 5 + NIST CSF mapping per system_id; CIS Controls v8 IG2/IG3 mapping per system_id; 2 Mermaid diagrams |
| Directive 1 — Structural Integrity | 10 | 1 file (804 lines): 25 findings covering broken cross-references, orphaned configurations (16 enterprise stubs), missing environment variables, dangling dependencies (24 manual DLLs), error handling gaps; Report Executive Summary + Attention Required Table |
| Directive 2 — Materiality Classification | 7 | 1 file (515 lines): ~222 components classified Material/Non-Material based on operational reliability impact; classification criteria aligned with COSO Information & Communication |
| Directive 3 — Code Quality Audit | 38 | 6 files (3,193 lines): code-quality-summary.md aggregate findings; security-domain-quality.md (Security.cs, ActiveDirectory, DuoUniversal — MD5 finding, session coupling); api-surface-quality.md (REST, SOAP, Admin API — input validation gaps); infrastructure-quality.md (Cache, Init, Sql, Error — coupling analysis); background-processing-quality.md (Scheduler, Email, timers — reentrancy); database-quality.md (SQL procedures, views, triggers) |
| Directive 4 — Dependencies | 7 | 1 file (503 lines): inter-system dependency mapping; shared utilities consumed by 3+ systems identified; Blast Radius Scores (Low/Medium/High); NIST CM-3 and SC-5 risk assessment; 2 Mermaid dependency graphs |
| Directive 5 — Documentation Coverage | 6 | 1 file (437 lines): WHY documentation verification for all Material components; blast radius and ownership documentation check; Mermaid coverage visualization |
| Directive 6 — Accuracy Validation | 8 | 1 file (878 lines): 282 samples across Static (1 each) and Dynamic (10–25 each) systems; 100% accuracy achieved; PASS status per COSO Principle 16 and ≥87% threshold |
| Directive 7 — Operational Artifacts | 18 | 3 files (1,121 lines): artifact-0 Global Executive Summary (COSO effectiveness statement, Top 5 Risk Table, NIST CSF posture); artifact-1 Operational Flowchart (8 Mermaid diagrams, NIST CSF swimlanes); artifact-2 Developer Contribution Guide (9 GATE: PASS/FAIL checkpoints) |
| Directive 8 — Presentation | 6 | 1 file (828 lines): self-contained Reveal.js v5.2.1 HTML presentation; 6 required slides (Title, State of Environment, Critical Risks, Compliance Scorecard, Technical Debt Impact, Prioritized Focus Areas); CSS derived from SplendidCRM App_Themes/Seven |
| docs/README.md — Navigation Index | 3 | 1 file (290 lines): audit suite navigation guide; framework glossary (COSO, NIST, CIS); report index; directive execution sequence Mermaid diagram; key terminology definitions |
| QA & Cross-Reference Fixes | 4 | 3 fix commits resolving 17+ findings: system_id consistency corrections, DLL count clarifications, cross-reference completeness, system count alignment across documents |
| **Total Completed** | **135** | **20 files, 11,116 lines, 23 commits** |

### 2.2 Remaining Work Detail

| Category | Base Hours | Priority | After Multiplier |
|---|---|---|---|
| Stakeholder & Peer Review — Review all 20 audit artifacts for accuracy, validate compliance mappings with subject matter experts, conduct executive review of D7/D8 artifacts, approve materiality classifications with business stakeholders | 10 | High | 12 |
| Framework Control ID Verification — Verify COSO Principle IDs (1–17) against official 2013 framework publication, verify NIST SP 800-53 Rev 5 control family codes and CSF mappings, verify CIS Controls v8 safeguard numbers | 3 | High | 4 |
| Presentation Cross-Browser & Accessibility — Test Reveal.js in Chrome, Firefox, Safari, Edge; validate PDF export; conduct accessibility review of HTML presentation | 2 | Medium | 2 |
| Documentation Deployment & CI Setup — Configure documentation hosting (GitHub Pages or equivalent), set up CI pipeline for Markdown linting (markdownlint) and link validation, configure automated broken-link detection | 3 | Medium | 4 |
| Mermaid Diagram Offline Export — Export 19 embedded Mermaid diagrams to static SVG/PNG for offline distribution and print-ready deliverables using @mermaid-js/mermaid-cli | 1 | Low | 1 |
| **Total Remaining** | **19** | | **23** |

### 2.3 Enterprise Multipliers Applied

| Multiplier | Value | Rationale |
|---|---|---|
| Compliance Requirements | 1.10x | Framework control accuracy is critical for audit credibility; COSO/NIST/CIS IDs must be exact — verification requires careful cross-referencing with official publications |
| Uncertainty Buffer | 1.10x | Stakeholder feedback scope is uncertain — review cycles may surface additional findings requiring documentation updates; first-time audit with no precedent for feedback volume |
| **Combined Multiplier** | **1.21x** | Applied to all remaining base hour estimates (19h × 1.21 = 23h) |

---

## 3. Test Results

| Test Category | Framework | Total Tests | Passed | Failed | Coverage % | Notes |
|---|---|---|---|---|---|---|
| File Existence Verification | Blitzy Validator | 20 | 20 | 0 | 100% | All 20 AAP-specified documentation files confirmed present |
| Content Size Validation | Blitzy Validator | 20 | 20 | 0 | 100% | All files >1KB; total 939KB across 20 files |
| Structural Requirements | Blitzy Validator | 11 | 11 | 0 | 100% | Report Executive Summary + Attention Required Table verified in all D1–D6 reports |
| Cross-Reference Validation | Blitzy Validator | 257 | 257 | 0 | 100% | All internal cross-references between documents resolve correctly |
| system_id Attribution | Blitzy Validator | 19 | 19 | 0 | 100% | SYS-* identifiers present across all applicable reports |
| COSO Reference Check | Blitzy Validator | 19 | 19 | 0 | 100% | COSO references confirmed in all 19 Markdown files |
| Framework Terminology Consistency | Blitzy Validator | 20 | 20 | 0 | 100% | Consistent COSO/NIST/CIS terminology format across all documents |
| Mermaid Diagram Syntax | Blitzy Validator | 19 | 19 | 0 | 100% | All 19 Mermaid diagrams use valid syntax |
| GATE Checkpoint Verification | Blitzy Validator | 9 | 9 | 0 | 100% | GATE 1–9 PASS/FAIL checkpoints confirmed in developer guide |
| Reveal.js Slide Count | Blitzy Validator | 6 | 6 | 0 | 100% | All 6 required slides confirmed (Title, Environment, Risks, Scorecard, Debt, Focus) |
| Reveal.js Browser Rendering | Blitzy Validator | 1 | 1 | 0 | 100% | Presentation renders correctly in browser with all 6 slides |
| HTML Structure Validation | Blitzy Validator | 1 | 1 | 0 | 100% | DOCTYPE, head, body, Reveal.js initialization confirmed |
| SplendidCRM CSS Provenance | Blitzy Validator | 1 | 1 | 0 | 100% | Seven theme colors documented with provenance from App_Themes |
| Git Working Tree Cleanliness | Blitzy Validator | 1 | 1 | 0 | 100% | No uncommitted changes; all work committed to correct branch |

**Summary:** 404 total validation checks executed, 404 passed, 0 failed — **100% pass rate** across all validation categories.

---

## 4. Runtime Validation & UI Verification

**Runtime Health:**

- ✅ All 20 documentation files exist on disk and are non-empty
- ✅ Git working tree is clean with all changes committed to branch `blitzy-45c7e69b-53a1-44b2-b26f-d1f67ad5c044`
- ✅ 23 Blitzy Agent commits in sequential directive order
- ✅ No forbidden files created (no source code modifications, no progress trackers)

**UI Verification (Reveal.js Presentation):**

- ✅ `docs/directive-8-presentation/risk-executive-presentation.html` loads in browser
- ✅ Reveal.js v5.2.1 initializes from CDN (`cdn.jsdelivr.net/npm/reveal.js@5.2.1`)
- ✅ All 6 required slides render correctly:
  - Slide 1: Title (SplendidCRM v15.2 — Risk Executive Summary)
  - Slide 2: State of the Environment (COSO/NIST CSF posture)
  - Slide 3: Critical Risks (High Blast Radius findings)
  - Slide 4: Compliance Scorecard (COSO/NIST/CIS grid)
  - Slide 5: Technical Debt Impact (testing gap, MD5, manual DLLs)
  - Slide 6: Prioritized Focus Areas (ranked remediation recommendations)
- ✅ SplendidCRM Seven theme CSS colors applied (documented with provenance)
- ✅ Slide navigation (arrow keys, mouse) functional

**Markdown Documentation Verification:**

- ✅ All 19 Markdown files use valid Markdown syntax (headers, tables, code blocks, links)
- ✅ 19 Mermaid diagrams use valid syntax renderable in GitHub/GitLab Markdown viewers
- ✅ 257 internal cross-references between documents all resolve correctly

**API/Integration Verification:**

- ⚠ N/A — This is a documentation-only project with no APIs, services, or integrations to verify

---

## 5. Compliance & Quality Review

| AAP Deliverable | Status | Quality Check | Notes |
|---|---|---|---|
| Directive 0 — System Registry (4 files) | ✅ Complete | 34 systems, COSO/NIST/CIS mapped | system_id registry established |
| Directive 1 — Structural Integrity (1 file) | ✅ Complete | Report Exec Summary + Attention Required Table | 25 findings with NIST/CIS/COSO attribution |
| Directive 2 — Materiality Classification (1 file) | ✅ Complete | ~222 components classified | Material/Non-Material criteria applied |
| Directive 3 — Code Quality (6 files) | ✅ Complete | 5 domain reports + summary | All Material components assessed |
| Directive 4 — Dependencies (1 file) | ✅ Complete | Blast Radius Scores assigned | NIST CM-3/SC-5 risk assessment included |
| Directive 5 — Documentation Coverage (1 file) | ✅ Complete | WHY documentation verified | Coverage gaps documented per COSO Principle 14 |
| Directive 6 — Accuracy Validation (1 file) | ✅ Complete | 282 samples, 100% accuracy | PASS per ≥87% threshold, COSO Principle 16 |
| Directive 7 — Operational Artifacts (3 files) | ✅ Complete | Exec summary + flowcharts + guide | 9 GATE points, NIST CSF swimlanes, Top 5 Risk Table |
| Directive 8 — Presentation (1 file) | ✅ Complete | 6 slides, Reveal.js v5.2.1 | SplendidCRM CSS provenance confirmed |
| Navigation Index (1 file) | ✅ Complete | Framework glossary + report index | All links validated |
| Assess-only mandate | ✅ Enforced | No SplendidCRM code modified | All 20 files in docs/ only |
| Sequential directive execution | ✅ Enforced | D0→D1→D2→D3→D4→D5→D6→D7→D8 | Git commit history confirms order |
| Mandatory report header format | ✅ Enforced | All D1–D6 reports | Report Exec Summary + Attention Required Table |
| system_id attribution | ✅ Enforced | All findings attributed | SYS-* IDs from D0 registry |
| COSO references in summaries | ✅ Enforced | All 19 Markdown files | Consistent "COSO Principle N" format |
| Framework authority hierarchy | ✅ Enforced | More restrictive applied | Conflicts flagged where identified |
| 9 GATE points | ✅ Enforced | Developer guide D7-A2 | GATE 1–9 PASS/FAIL confirmed |
| SplendidCRM CSS in Reveal.js | ✅ Enforced | Seven theme colors | Provenance documented in HTML |

**Fixes Applied During Validation:**

| Commit | Findings Resolved | Description |
|---|---|---|
| `c703eb2` | 14 | Cross-reference accuracy, framework terminology consistency, system_id alignment across 11 files |
| `6c83775` | 1 | Invalid system_id SYS-BG-SCHEDULER replaced with SYS-SCHEDULER in D7-A1 flowchart |
| `e20eb93` | 3 | System count consistency, DLL count clarification, cross-reference completeness |

---

## 6. Risk Assessment

| Risk | Category | Severity | Probability | Mitigation | Status |
|---|---|---|---|---|---|
| Framework control ID inaccuracy — COSO/NIST/CIS IDs may not match exact official publication numbers | Technical | Moderate | Medium | Human expert verification against official COSO 2013, NIST SP 800-53 Rev 5, CIS Controls v8 publications | Open — requires human review |
| Audit findings staleness — codebase changes after audit make findings outdated | Operational | Moderate | High | Establish re-audit schedule; tag audit to specific commit hash (e20eb93) | Open — requires process |
| Reveal.js CDN dependency — presentation requires internet for Reveal.js v5.2.1 assets | Technical | Minor | Medium | Download Reveal.js assets for offline use or embed inline | Open — low priority |
| Sensitive security findings exposure — audit reports detail MD5 hashing, disabled request validation, and other vulnerabilities | Security | Moderate | Medium | Restrict access to audit documentation; apply need-to-know distribution | Open — requires access controls |
| No automated documentation freshness checks — no CI pipeline to detect broken links or stale content | Operational | Minor | High | Configure markdownlint and link-checking CI job | Open — requires CI setup |
| Mermaid diagram rendering inconsistency — 19 diagrams may render differently across Markdown viewers | Technical | Minor | Low | Export to static SVG/PNG for consistent rendering; test in target viewers | Open — low priority |
| Single-reviewer validation — only automated validation performed, no human subject matter expert review | Operational | Moderate | High | Schedule peer review with compliance and development team leads | Open — requires scheduling |
| Incomplete materiality classification — some borderline components may be misclassified | Technical | Minor | Low | Business stakeholder review of materiality criteria and edge cases | Open — requires review |

---

## 7. Visual Project Status

```mermaid
pie title Project Hours Breakdown
    "Completed Work" : 135
    "Remaining Work" : 23
```

**Hours by Directive (Completed):**

| Directive | Hours | % of Completed |
|---|---|---|
| D0 — System Registry | 28 | 20.7% |
| D1 — Structural Integrity | 10 | 7.4% |
| D2 — Materiality | 7 | 5.2% |
| D3 — Code Quality | 38 | 28.1% |
| D4 — Dependencies | 7 | 5.2% |
| D5 — Documentation Coverage | 6 | 4.4% |
| D6 — Accuracy Validation | 8 | 5.9% |
| D7 — Operational Artifacts | 18 | 13.3% |
| D8 — Presentation | 6 | 4.4% |
| README + QA | 7 | 5.2% |

**Remaining Work by Priority:**

| Priority | Hours | Categories |
|---|---|---|
| High | 16 | Stakeholder & Peer Review (12h), Framework Verification (4h) |
| Medium | 6 | Presentation Testing (2h), Documentation Deployment (4h) |
| Low | 1 | Mermaid Diagram Export (1h) |

---

## 8. Summary & Recommendations

**Achievement Summary:**

The SplendidCRM v15.2 Codebase Audit Documentation Suite is **85.4% complete** (135 hours completed out of 158 total project hours). All 20 AAP-specified documentation artifacts have been created, validated, and committed — totaling 11,116 lines of deeply technical, compliance-mapped audit content organized into 9 sequential Directives (0–8). The audit suite successfully decomposes 34 systems, classifies ~222 components by materiality, audits all Material components for code quality across 5 domains, maps all findings to COSO Principles 1–17, NIST SP 800-53 Rev 5, NIST CSF, and CIS Controls v8, and achieves 100% accuracy in statistical validation (282 samples, PASS status). The Reveal.js risk executive presentation renders correctly with SplendidCRM theme CSS, and all 257 internal cross-references resolve with zero broken links.

**Remaining Gaps:**

The 23 remaining hours (14.6% of total) consist entirely of human-dependent path-to-production activities: (1) stakeholder and peer review of the audit content by compliance experts and developers (12h), (2) verification of framework control IDs against official publications (4h), (3) cross-browser and accessibility testing of the Reveal.js presentation (2h), (4) documentation hosting and CI pipeline setup (4h), and (5) Mermaid diagram offline export (1h). No additional documentation creation or codebase analysis is required.

**Critical Path to Production:**

1. **Week 1:** Schedule and begin peer review with compliance team (COSO/NIST/CIS control ID verification) and development team leads (code quality finding validation)
2. **Week 2:** Complete stakeholder review; incorporate feedback; set up documentation hosting
3. **Week 3:** Finalize presentation testing; configure CI pipeline; export Mermaid diagrams; deliver to stakeholders

**Production Readiness Assessment:**

The documentation suite meets all structural and content requirements specified in the AAP. All 20 files are present, all mandatory formatting patterns are applied (Report Executive Summary, Attention Required Table, system_id attribution, COSO references), and all cross-references are validated. The primary gate to production release is human peer review and framework control ID verification — both standard pre-publication activities for compliance documentation.

---

## 9. Development Guide

### System Prerequisites

| Prerequisite | Version | Purpose |
|---|---|---|
| Any modern web browser | Chrome 90+, Firefox 90+, Safari 15+, Edge 90+ | View Reveal.js presentation |
| Any Markdown viewer | GitHub, GitLab, VS Code with Markdown Preview, any text editor | View audit reports |
| Git | 2.30+ | Clone and navigate repository |
| Node.js (optional) | 16.20+ | Only needed for Mermaid CLI offline diagram export |
| npm/npx (optional) | 8+ | Only needed for Mermaid CLI |

### Environment Setup

No build step, virtual environment, database, or external service is required. The documentation suite is entirely static.

```bash
# Clone the repository
git clone <repository-url>
cd <repository-name>

# Switch to the working branch
git checkout blitzy-45c7e69b-53a1-44b2-b26f-d1f67ad5c044

# Verify all 20 documentation files exist
find docs/ -type f | wc -l
# Expected output: 20

# Verify total line count
find docs/ -type f -exec wc -l {} + | tail -1
# Expected output: 11116 total
```

### Viewing the Documentation

**Markdown Reports (Directives 0–7):**

```bash
# Open the navigation index first
# In VS Code:
code docs/README.md

# Or view in terminal:
cat docs/README.md

# Navigate to specific directives:
cat docs/directive-0-system-registry/system-registry.md
cat docs/directive-1-structural-integrity/structural-integrity-report.md
cat docs/directive-3-code-quality/code-quality-summary.md
```

For best rendering of Mermaid diagrams and tables, view in GitHub/GitLab or VS Code with the Markdown Preview and Mermaid extensions installed.

**Reveal.js Presentation (Directive 8):**

```bash
# Open directly in a web browser:
# On macOS:
open docs/directive-8-presentation/risk-executive-presentation.html

# On Linux:
xdg-open docs/directive-8-presentation/risk-executive-presentation.html

# On Windows:
start docs/directive-8-presentation/risk-executive-presentation.html
```

Navigate slides with arrow keys or mouse. The presentation loads Reveal.js v5.2.1 from CDN — internet access is required on first load.

### Verification Steps

```bash
# 1. Verify all 20 files are present
find docs/ -type f | sort
# Expected: 20 files listed (19 .md + 1 .html)

# 2. Verify no empty files
find docs/ -type f -size 0
# Expected: no output (all files have content)

# 3. Verify Report Executive Summaries in D1-D6
find docs/ -name "*.md" -exec grep -l "Report Executive Summary\|Attention Required" {} \;
# Expected: 12 files (D1-D6 reports + README)

# 4. Verify system_id attribution
find docs/ -name "*.md" -exec grep -l "SYS-" {} \;
# Expected: 19 files

# 5. Verify COSO references
find docs/ -name "*.md" -exec grep -l "COSO" {} \;
# Expected: 19 files

# 6. Verify 9 GATE checkpoints
grep "^## GATE" docs/directive-7-operational-artifacts/artifact-2-developer-contribution-guide.md
# Expected: GATE Summary + GATE 1 through GATE 9

# 7. Verify 6 Reveal.js slides
grep -c "<section" docs/directive-8-presentation/risk-executive-presentation.html
# Expected: 6

# 8. Verify Mermaid diagram count
grep -r '```mermaid' docs/ | wc -l
# Expected: 19
```

### Optional: Mermaid Diagram Offline Export

```bash
# Install Mermaid CLI (optional — only for offline SVG/PNG export)
npx @mermaid-js/mermaid-cli@10.6.1 -i docs/directive-0-system-registry/system-registry.md -o diagram.svg

# Or install globally:
npm install -g @mermaid-js/mermaid-cli@10.6.1
mmdc -i input.mmd -o output.svg
```

### Troubleshooting

| Issue | Resolution |
|---|---|
| Mermaid diagrams not rendering | View in GitHub/GitLab or install VS Code Mermaid extension |
| Reveal.js presentation blank | Check internet connection for CDN access; open browser console for errors |
| Tables not aligned | View in a proper Markdown renderer, not raw text |
| Cross-references broken | Ensure you are at the repository root; all paths are relative to `docs/` |

---

## 10. Appendices

### A. Command Reference

| Command | Purpose |
|---|---|
| `find docs/ -type f \| wc -l` | Count total documentation files |
| `find docs/ -type f -exec wc -l {} +` | Count lines per file |
| `grep -r '```mermaid' docs/ \| wc -l` | Count embedded Mermaid diagrams |
| `grep -c "<section" docs/directive-8-presentation/risk-executive-presentation.html` | Count Reveal.js slides |
| `find docs/ -name "*.md" -exec grep -l "COSO" {} \;` | Find files with COSO references |
| `find docs/ -name "*.md" -exec grep -l "SYS-" {} \;` | Find files with system_id references |
| `npx @mermaid-js/mermaid-cli@10.6.1 -i input.md -o output.svg` | Export Mermaid diagrams to SVG |

### B. Port Reference

No ports are used by this project. The documentation suite is entirely static. The Reveal.js presentation opens as a local file in a browser (no server required).

### C. Key File Locations

| File | Location | Purpose |
|---|---|---|
| Navigation Index | `docs/README.md` | Entry point and audit suite table of contents |
| System Registry | `docs/directive-0-system-registry/system-registry.md` | system_id reference for all reports |
| COSO Mapping | `docs/directive-0-system-registry/coso-mapping.md` | COSO Principles 1–17 per system |
| NIST Mapping | `docs/directive-0-system-registry/nist-mapping.md` | NIST SP 800-53 + CSF per system |
| CIS Mapping | `docs/directive-0-system-registry/cis-mapping.md` | CIS Controls v8 per system |
| Structural Integrity | `docs/directive-1-structural-integrity/structural-integrity-report.md` | Boundary scan findings |
| Materiality | `docs/directive-2-materiality/materiality-classification.md` | Material/Non-Material classification |
| Code Quality Summary | `docs/directive-3-code-quality/code-quality-summary.md` | Aggregate quality findings |
| Security Quality | `docs/directive-3-code-quality/security-domain-quality.md` | Security domain deep audit |
| API Quality | `docs/directive-3-code-quality/api-surface-quality.md` | API surface deep audit |
| Infrastructure Quality | `docs/directive-3-code-quality/infrastructure-quality.md` | Core infrastructure audit |
| Background Quality | `docs/directive-3-code-quality/background-processing-quality.md` | Timer/scheduler audit |
| Database Quality | `docs/directive-3-code-quality/database-quality.md` | SQL layer audit |
| Dependency Audit | `docs/directive-4-dependency-audit/cross-cutting-dependency-report.md` | Blast radius analysis |
| Doc Coverage | `docs/directive-5-documentation-coverage/documentation-coverage-report.md` | WHY documentation gaps |
| Accuracy Validation | `docs/directive-6-accuracy-validation/accuracy-validation-report.md` | Sampling results |
| Executive Summary | `docs/directive-7-operational-artifacts/artifact-0-global-executive-summary.md` | Stakeholder narrative |
| Operational Flowchart | `docs/directive-7-operational-artifacts/artifact-1-operational-flowchart.md` | NIST CSF swimlanes |
| Developer Guide | `docs/directive-7-operational-artifacts/artifact-2-developer-contribution-guide.md` | 9 GATE checkpoints |
| Presentation | `docs/directive-8-presentation/risk-executive-presentation.html` | Risk executive slides |

### D. Technology Versions

| Technology | Version | Role in Project |
|---|---|---|
| Reveal.js | 5.2.1 | Presentation framework (CDN-loaded in HTML) |
| Mermaid | Inline (viewer-rendered) | Diagram notation in Markdown files |
| Markdown | CommonMark | Documentation format for all audit reports |
| HTML5 | Standard | Presentation file format |

**Audited Codebase Technology Stack (read-only reference):**

| Technology | Version | Layer |
|---|---|---|
| ASP.NET | 4.8 (.NET Framework) | Backend application |
| C# | 7.x | Backend language |
| React | 18.2.0 | Primary client SPA |
| TypeScript | 5.3.3 | React SPA language |
| Angular | ~13.3.0 | Experimental client |
| jQuery | 1.4.2–2.2.4 | Legacy HTML5 client |
| SQL Server | 2008+ Express | Database |
| SignalR | Server: 1.2.2 / Client: 8.0.0 | Real-time messaging |
| Bootstrap | 5.3.2 (React) / 3.3.7 (legacy) | UI framework |
| MobX | 6.12.0 | React state management |
| Webpack | 5.90.2 | React build tool |

### E. Environment Variable Reference

No environment variables are required for the documentation suite. All documentation files are static and self-contained.

**Note:** The audited SplendidCRM codebase uses `Web.config` for configuration rather than environment variables. This is documented as a finding in the Directive 1 Structural Integrity report.

### F. Developer Tools Guide

| Tool | Purpose | Installation |
|---|---|---|
| VS Code + Markdown Preview Enhanced | Best Markdown rendering with Mermaid support | `code --install-extension shd101wyy.markdown-preview-enhanced` |
| VS Code + Mermaid Extension | Mermaid diagram preview | `code --install-extension bierner.markdown-mermaid` |
| markdownlint-cli | Markdown linting for CI | `npm install -g markdownlint-cli` |
| @mermaid-js/mermaid-cli | Offline Mermaid to SVG/PNG | `npm install -g @mermaid-js/mermaid-cli@10.6.1` |
| markdown-link-check | Broken link detection | `npm install -g markdown-link-check` |

### G. Glossary

| Term | Definition |
|---|---|
| AAP | Agent Action Plan — the primary directive defining all project requirements |
| COSO | Committee of Sponsoring Organizations of the Treadway Commission — governance framework with 5 components and 17 principles |
| NIST SP 800-53 | National Institute of Standards and Technology Special Publication 800-53 — comprehensive security and privacy control catalog |
| NIST CSF | NIST Cybersecurity Framework — 5-function risk management structure (Identify, Protect, Detect, Respond, Recover) |
| CIS Controls v8 | Center for Internet Security Controls — prioritized cybersecurity safeguards (IG2/IG3 implementation groups used) |
| system_id | Unique identifier (SYS-*) assigned to each system in the Directive 0 registry |
| Material | Component classification indicating significant impact on operational reliability |
| Non-Material | Component classification indicating limited impact on operational reliability |
| Static | System classification: configuration or infrastructure that changes infrequently (1 sample in validation) |
| Dynamic | System classification: runtime behavior with variable outputs (10–25 samples in validation) |
| Blast Radius Score | Low/Medium/High classification of cross-cutting dependency risk per COSO Principle 9 |
| GATE | Checkpoint in the Developer Contribution Guide — evaluates PASS or FAIL status for code contributions |