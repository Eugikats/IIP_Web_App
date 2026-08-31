# IIP Release 1 Acceptance and Release Gate Register

| Field | Value |
|---|---|
| Version | 2.8 |
| Date | 2026-08-10 |
| Status | Proposed thresholds; approval and execution evidence pending |
| Requirement source | `iip_release_1_requirements_baseline.md` |

## Evidence rule

An acceptance result is valid only when it identifies requirement/test ID, artifact and configuration versions, environment, tenant/workspace/test data class, command or procedure, expected/actual result, date, executor, limitations, defects/waivers, and required approver. A local pass is not deployed/UAT/production evidence.

## Gate levels

| Gate | Purpose | Required evidence | Decision owners |
|---|---|---|---|
| G0 — Requirements ready | Safe for estimation/design/test preparation | Baseline, scope, glossary, impact assessment, open-question disposition, traceability plan | Product, Insurance, Operations, Security/Privacy, Architecture, Delivery |
| G1 — Source complete | Coherent implementation candidate | Code/contracts/docs/tests exist; no known invariant violation; CI/local affected checks pass | Engineering, Architecture, QA |
| G2 — Protected staging ready | Safe to expose to target-insurer testers | Real auth/storage/providers or honest `Not configured`; migrations; security; telemetry; recovery/runbooks | Security, Architecture, Operations, Delivery |
| G3 — UAT ready/accepted | Representative users can validate the workflow | Multi-role authenticated UAT, client configuration, representative safe cases, defects and known limits | Product, Insurance, Operations, Client sponsor, QA |
| G4 — Controlled pilot ready | Limited real use with monitoring and rollback | Legal/privacy/data approvals, impact assessment, baseline/targets, support/incident/rollback, provider/load evidence | Six internal functions + client legal/security/operations sponsor |
| G5 — Production candidate | Candidate for wider approved operation | Pilot outcome, full P0 traceability, performance/resilience/accessibility/usability/restore/operational evidence, residual-risk acceptance | Executive release authority + client authority |

## Cross-cutting measurable gates

Standards basis: [WCAG 2.2](https://www.w3.org/TR/WCAG22/) defines the Level AA accessibility target including 320 CSS-pixel reflow; the current [Core Web Vitals thresholds](https://web.dev/articles/defining-core-web-vitals-thresholds) define LCP/INP/CLS “good” values at the 75th percentile. [Azure reliability guidance](https://learn.microsoft.com/en-us/azure/well-architected/reliability/metrics) requires workload-specific SLO/RTO/RPO agreement and testing, so IIP recovery values remain provisional until business-impact approval.

| ID | Gate | Pass threshold | Evidence method | Current status |
|---|---|---|---|---|
| AC-AUTH-01 | G1+ | 100% sensitive Release 1 routes enforce current server-resolved membership/permission/scope; all cross-tenant and revoked-membership tests pass | API security suite + route inventory | Code/tests exist; full route evidence review pending |
| AC-HUM-01 | G1+ | 100% executable consequential paths contain an unavoidable authorized-human gate; system/agent actor cannot decide/execute them | Domain graph, API, worker and alternate-path tests | Core remediation reported; release-wide proof pending |
| AC-SIM-01 | G1+ | Zero protected-runtime mock/fallback/fake success/seeded-business-data activations; invalid configuration fails readiness/action safely | Configuration matrix, binary/source scan, deployed smoke | Pending per target environment |
| AC-GRD-01 | G3+ | 100% consequential outputs identify current source references and required versions; zero unsupported material claim in severe test corpus | Evaluation + reviewer task test | Corpus/tolerance pending |
| AC-COR-01 | G1+ | 100% reviewed source corrections invalidate affected downstream derived work before reliance | Domain/API regression tests | Implemented tests reported; traceability reconciliation pending |
| AC-DATA-01 | G2+ | Zero cross-tenant disclosure; zero prohibited sensitive payload in sampled logs/audit/telemetry/exports | Security/privacy tests and sampling | Pending protected environment |
| AC-SEARCH-01 | G1/G3+ | Global search filters tenant/workspace, case assignment and object permission before matching/counting; returns zero prohibited fields; exact references rank first; raw queries are absent from audit; keyboard, empty/partial/error/open/return states pass | API contract tests, audit sampling, automated/manual accessibility and protected multi-role browser journey | Local API tests and portal build pass; protected staging, audit sampling, accessibility, practitioner validation and named approval pending |
| AC-A11Y-01 | G3+ | Critical flows meet WCAG 2.2 AA; no open critical/serious barrier in keyboard, screen-reader, focus, contrast, errors, status, 200% text and 400%/320 CSS px reflow tests | Automated + manual accessibility report | Not executed |
| AC-USE-01 | G3+ | ≥90% representative users complete every critical task unassisted; zero unrecovered consequential error | Moderated usability/UAT with sample/context | Participant evidence absent |
| AC-REC-01 | G3+ | ≥90% recover unassisted from injected upload/provider/stale/session errors; 100% preserve committed work and prevent duplicate consequential effect | Failure-recovery task tests | Not executed |
| AC-L10N-01 | G3+ | Unicode/original text/language metadata round-trip without loss; unsupported language produces limitation + human review and zero invented extraction | Mixed/unsupported-language contract and UI tests | Language matrix pending |
| AC-PERF-01 | G2+ | At representative authenticated load: health p95 ≤300 ms, claims list p95 ≤800 ms, detail p95 ≤1,200 ms, unexpected error <1%; p99 reported | Near-region load test with telemetry correlation | **Fail:** prior dev claims list p95 1,277–1,336 ms; representative staging pending |
| AC-WEB-01 | G3+ | 75th percentile LCP ≤2.5 s, INP ≤200 ms, CLS ≤0.1 on critical supported sessions | RUM or representative device/network lab | Not executed |
| AC-LOW-01 | G3+ | At 400/100 kbps, 600 ms RTT, 2% loss: critical metadata usable ≤10 s; no false completion; retry/resume remains available | Throttled browser/API tests | Proposed profile; participant validation pending |
| AC-NET-01 | G3+ | 30-second disconnection does not lose committed work or duplicate effect; resumable operations recover or explain safe restart | Network interruption test | Not executed |
| AC-DR-01 | G4+ | Restore rehearsal meets provisional RPO ≤15 min and RTO ≤4 h for approved critical data/configuration | Backup/restore exercise | Values and execution pending business approval |
| AC-OPS-01 | G2+ | Every critical dependency has health/state, alert, named owner, runbook, safe user state, bounded retry and manual recovery | Outage/DLQ/runbook drill | Partial; live-provider drill pending |
| AC-WA-01 | G2/G3+ | Verified inbound WhatsApp webhook and server-side tenant mapping; replay-safe case/manual-review correlation; reviewed outbound send; explicit provider acknowledgement/delivery/failed/unknown states; zero simulated success | Provider contract/security tests plus real-provider protected-staging and UAT journey | Thread/outbound/delivery source and local tests complete; real-provider protected-staging, UAT and named approvals pending |
| AC-UAT-01 | G3+ | All P0 critical journeys pass for approved roles and representative cases; zero open critical defect; high defects fixed or explicitly blocked, never silently waived | Signed UAT pack | Not executed |
| AC-TRC-01 | G4+ | 100% P0 and activated conditional requirements link design, API/data, implementation, tests and release evidence; no orphan route/screen/test | RTM validation | Matrix upgrade pending validation |
| AC-FLOW-01 | G1+ | Every `FLW-R1-*` step has an actor, entry/exit, current permission/enforcement seam, state owner, feedback, audit action or explicit gap; every nonterminal/exception state has a safe exit | Specification validator + architecture/UX/QA review | Companion pack drafted; mechanical and review validation pending |
| AC-LFW-01 | G1+ | Every canonical `IA-SCR-*` screen maps loading, empty, error, permission, partial, success and recovery variants; each variant names preserved work, exact owner/state, accessible feedback and a safe exit; P0 layouts cover wide and 320px/reflow composition before visual expansion | Release 1 specification validator plus Product/Design/Architecture/Accessibility review of rendered low-fidelity layouts | **Partial:** 34 screens × 7 states mechanically mapped; interactive critical-flow prototype rendered across 12 surface/mode entries and three structures; exact screen-state annotation, cross-functional review and participant evidence pending |
| AC-RSP-01 | G1/G3+ | Every P0 layout family preserves task priority, semantic/focus order, current authority, evidence/state, safe input and recovery across desktop, tablet and mobile behavioral bands, 200%/400% zoom, 320 CSS px, orientation and input changes; no critical action/status is lost, obscured, hover-only or replayed by reflow | Responsive contract validator; rendered-wireframe review; browser computed-layout, keyboard/touch/screen-reader/zoom/reflow suite; physical tablet/mobile and practitioner tasks | **Partial:** source-complete reflow contract and interactive wide/intermediate/compact prototype exist; exact browser/device anchors, current portal parity, physical-device/accessibility and practitioner evidence pending |
| AC-CPLX-01 | G1/G3+ | Queue, evidence-review, approval-cockpit, table, panel and dialog patterns each define purpose, anatomy, state/data ownership, task flow, selection/filter/edit/persistence rules, all seven screen states, responsive/input/accessibility behavior, goal-based analytics and normal/negative/recovery tests; zero bulk consequential decision, hidden evidence/authority, lost draft/context or false success | Complex-pattern specification validator; rendered P0 state review; component/API parity audit; keyboard/touch/screen-reader/reflow browser suite; protected multi-role scenarios and practitioner tasks | **Partial:** six pattern contracts and 18 scenarios are source-complete; rendering, implementation parity, accessibility, protected-runtime and practitioner evidence pending |
| AC-EVD-01 | G1/G3+ | Missing, unverified, low-confidence, conflicting, stale, superseded, unauthorized, unsupported-language, duplicate, corrupt and unsafe evidence each reach the documented owned path; ineligible evidence cannot support execution | Domain/API decision-tree tests plus protected browser/UAT scenarios | Documentation mapped; complete runtime matrix pending |
| AC-SOD-01 | G1/G3+ | Every consequential/review/disclosure action maps to explicit server permission, object/state guard, delegation and separation-of-duty policy; zero unexplained role permission or bypass | Route/action/role inventory plus negative, revoked, delegated and cross-role tests | `PERM-GAP-01`–`05` open; protected consequential UAT blocked |
| AC-INT-01 | G1+ | 100% of registered `ACT-R1-*` interactions link atomic requirements, measurable acceptance, current permission/enforcement, audit action or explicit gap, flow/state/screen/API seams, test/UAT evidence, and readiness | Interaction RTM validator plus architecture/Product/Security/QA review | Interaction RTM created; approval and protected evidence pending |
| AC-PRE-01 | G1/G3+ | 100% applicable consequential/external/disclosure/export/destructive actions show exact purpose, target, effect, prerequisites, authority, reversibility and audit consequence; zero preselected or generic consequential confirmation | Contract review plus permitted/denied/stale browser journeys | Design mapped; component/runtime verification partial |
| AC-ACT-01 | G1/G3+ | Every critical control covers default, disabled, loading/pending, authoritative success, failure, retry eligibility and duplicate submit; zero false success or second effect from repeated activation | Component/browser/API state matrix with delayed and repeated-submit injection | Design mapped; complete runtime matrix pending |
| AC-CON-01 | G1/G3+ | 100% mutable critical writes either verify current version/state or document a safe equivalent guard; concurrent stale writes produce explicit conflict, preserve safe input, and never silently overwrite | Concurrent API/browser tests across queue, evidence, approval, communication, conversation and export families | Conversation contract current; `PAR-GAP-01` open elsewhere |
| AC-AUD-01 | G1/G2+ | Every critical interaction maps to an existing audit `ActionName`, an approved non-business telemetry owner, or explicit `AUD-GAP-*`; sampled committed events contain all applicable safe reconstruction fields and zero prohibited payload | Source validator, API tests, protected audit/telemetry sampling and correlation | Current events mapped; `AUD-GAP-01`–`05` and protected sampling remain open |
| AC-ASR-01 | G1/G2/G3+ | Assessor identity, minimum disclosure, malicious attachment/quarantine, forwarding/non-revocation, stale token/link, and tenant/provider-isolation matrices pass with zero unauthorized release, zero quarantine bypass, zero bearer evidence URL/SAS, zero cross-tenant exposure, and exact-state audit reconstruction | Threat-assessment contract review; API/domain/provider negative tests; malware/parser evidence; protected multi-role/Graph journeys; Exchange scope proof; audit sampling; residual-risk approval | **Blocked:** no assessor runtime/permission/audit contract; current Graph attachment path lacks equivalent size/type validation and verified quarantine/malware clearance; forwarding policy and protected mailbox-scope evidence absent |
| AC-ETH-01 | G1/G3+ | Every assessor disclosure item maps to an approved claim-specific question and necessity reason; zero prohibited/unnecessary item is released; claimant notice is useful across approved language/access needs; materially contested assessor evidence blocks new reliance until authorized reconsideration; zero unapproved training, ranking, retrieval-authority or scoring reuse; fairness/burden monitoring and residual external-handling risk are approved | Ethical-review contract audit; pack/notice comprehension tests; mediated claimant/independent-review UAT; source/config/data-use inventory; protected audit sampling; Privacy/Claims/Responsible-AI residual-risk approval | **Blocked:** disclosure taxonomy/manifest, claimant notice, external-professional terms, contested-evidence state, fairness monitoring, implementation and claimant/practitioner evidence absent |
| AC-A11Y-02 | G1/G3+ | Every modal primitive passes initial focus, forward/reverse containment, inoperable background, safe Escape/cancel boundary, invoker/fallback return, and reduced-motion checks | Keyboard browser suite plus manual supported-screen-reader verification | **Fail:** local diagnostic found entry/containment/return defects (`A11Y-GAP-01`) |
| AC-A11Y-03 | G1/G3+ | Zero critical/serious accessible-name, role, state, label, instruction, validation association, status, alert or progress-semantic barrier across every critical interaction/state | Axe plus NVDA/Chrome and one additional supported screen-reader/browser journey | **Fail:** local diagnostic found critical name/label/select and incomplete announcement defects |
| AC-A11Y-04 | G1/G3+ | 100% primary/high-impact/repeated/icon targets are at least 44 by 44 CSS pixels; zero unexplained target below WCAG 2.2 minimum at desktop, mobile-emulated and physical-touch verification | Programmatic bounds audit plus physical touch-device task test | **Fail:** local diagnostic found many sub-44 and some sub-24 targets |
| AC-A11Y-05 | G1/G3+ | Critical rendered states meet AA text/control/focus contrast, use non-color cues, retain visible focus, and remain operable in Windows forced colors with zero critical/serious barrier | Rendered contrast audit, keyboard focus audit and forced-colors manual/browser checks | **Fail:** local diagnostic found serious contrast and inconsistent focus-indicator defects |
| AC-A11Y-06 | G1/G3+ | With reduced motion enabled, critical surfaces contain zero nonessential translation/scale/parallax/pulsing/animated progress/transition and retain equivalent immediate semantic feedback | Reduced-motion computed-style/browser checks plus manual task walkthrough | **Fail:** local diagnostic found retained dialog/hover transitions |
| AC-APR-01 | G0/G4/G5 | Every required function records a named decision for exact versions; any pending/rejected/expired decision blocks gate | Approval records | Pending |

## Critical journey acceptance

| Journey | Required paths |
|---|---|
| Staff entry | authorized, no membership, wrong workspace, revoked mid-session, session expiry/re-entry |
| Case intake/queue | valid, invalid, duplicate, empty queue, filter/search, assignment conflict, escalation |
| Global search | exact/partial reference, assigned-only/all-claims, approval permitted/restricted, cross-tenant, no-authorized-results, partial/error, keyboard submit, target access recheck, back-to-results |
| Document evidence | supported, corrupt/oversized/unsafe, duplicate, provider absent/timeout/empty, mixed/unsupported language |
| Review/correction | accept, correct, reject, return, escalate, concurrent edit, correction after downstream preparation |
| Assessor handoff | authorized immutable minimum pack, denied/unapproved disclosure, manual export/attestation, optional Graph accepted/unknown/reconciled, exact-version acknowledgement, failed/expired/reissued referral, duplicate/unmatched/late/unsafe report return, human evidence review, conflict, clarification and superseding revision; no portal/core write-back |
| Completeness/follow-up | configured rule, no rule, missing/conflicting/stale evidence, draft approval/reject/send failure |
| WhatsApp intake/follow-up | valid/invalid signature, wrong number/mapping, duplicate/replay, matched/unmatched message/document, restricted content, approved/rejected/stale outbound draft, provider absent/timeout/callback duplication, delivered/failed/unknown reconciliation |
| Human decision | approve, reject, return, escalate, expired/mismatched approval, alternate-path bypass attempt |
| Execution/reconciliation | configured success, `Not configured`, timeout before/after external effect, duplicate retry, compensation/manual intervention |
| Contestability | request, acknowledgement, owned review, correction, downstream propagation, closure/remedy |
| Audit/export | permitted, restricted, cross-tenant, large/partial case, safe metadata, version reconstruction |
| Interruption/accessibility | keyboard/screen reader/reflow, slow/lost network, leave/resume, stale evidence, error recovery |

## Non-acceptance conditions

- Missing named authority or target-insurer configuration is treated as an invented default.
- AI/system can complete a consequential outcome or reviewer cannot meaningfully disagree/remedy.
- Provider absence/failure becomes empty/fake success.
- Cross-tenant or customer-facing output leaks restricted information.
- Corrected evidence leaves executable stale downstream work.
- Any P0 requirement lacks a test or release-evidence link.
- Performance target is relaxed after failure without approved evidence/change control.
- Accessibility/usability result excludes a critical flow or known severe barrier.
- “Approved” lacks named person, role, exact version, date and conditions.

## Decision record

| Gate/version | Decision | Conditions/open defects | Named approvers | Date/expiry | Evidence pack |
|---|---|---|---|---|---|
| G0 / baseline 0.11 | Pending | Six-function review required | — | — | — |
| G1 | Not assessed | Source/diff/test review after approval | — | — | — |
| G2 | Blocked | Real provider, migrations, protected staging and telemetry gaps | — | — | RF-10 evidence pack |
| G3 | Blocked | Target insurer, roles/configuration, authenticated UAT absent | — | — | — |
| G4 | Blocked | Legal/privacy/baselines/impact/support/pilot approvals absent | — | — | — |
| G5 | Blocked | Pilot and full evidence absent | — | — | — |

No gate is currently approved by this document.
