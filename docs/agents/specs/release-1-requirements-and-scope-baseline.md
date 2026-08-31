# Release 1 Requirements and Scope Baseline

**Tracker ID:** IIP-R1-REQ-001
**Triage:** `ready-for-agent`
**Activation status:** Requirements gap remediation ready; scope freeze/pilot/production blocked on recorded approvals and evidence
**Authority:** `AGENTS.md`, product intent, Release 1 requirements baseline, scope/acceptance/traceability registers

## Problem Statement

IIP has broad PRD, API, UX, test, scope, risk and release documents created at different times. They mix aspirational modules with Release 1, use stale routes and state names, leave quality thresholds vague, treat some client/legal questions as harmless defaults, and cannot trace every requirement through design, data/API, implementation, tests and release proof. Stakeholders cannot safely approve scope, engineers can implement against conflicting contracts, and a document-heavy project can appear ready without practitioner, accessibility, performance, recovery, UAT or formal approval evidence.

## Solution

Use one atomic Release 1 requirements baseline for non-injury motor own-damage evidence review and approval preparation, with WhatsApp intake and reviewed follow-up as a mandatory channel capability. Maintain scope, acceptance, traceability, API/data ownership, responsible-algorithm impact, risks, decisions and stakeholder approvals as linked views. Prefer current code/OpenAPI over stale endpoint prose. Preserve insurer human authority, tenant isolation, current evidence, zero simulation, correction propagation, multilingual sources, accessibility and recoverability. Agents may close mapped implementation/test/document gaps; target-insurer, legal, live-provider, UAT and approval gates require their named human owners.

## User Stories

1. As a claims worker, I want my current membership and workspace resolved server-side, so that I only handle authorized insurer work.
2. As a claims worker, I want to create one structured motor case from valid intake, so that evidence work starts from a durable record.
3. As a supervisor, I want a real work queue showing owner, status, gaps, approval and SLA state, so that I can route attention safely.
4. As a claims worker, I want one current case workspace, so that I do not reconstruct work across hidden sources.
5. As a document reviewer, I want unsafe or unsupported uploads rejected honestly, so that they cannot contaminate downstream work.
6. As a document reviewer, I want extraction linked to the original source and provider/version, so that I can verify each field.
7. As a document reviewer, I want to accept, correct, reject, return or escalate extracted fields, so that errors have safe recovery.
8. As a claims worker, I want completeness based on approved insurer rules, so that IIP does not invent evidence requirements.
9. As a claims worker, I want a correction to invalidate stale downstream work, so that obsolete evidence cannot be relied upon.
10. As an approver, I want summaries and proposals grounded in current evidence, so that I understand their basis and limitations.
11. As an approver, I want the exact proposed action, evidence, risk and versions, so that approval is meaningful rather than generic.
12. As an approver, I want approve, reject, return and escalate choices with reasons, so that I retain real authority.
13. As an operator, I want execution to recheck permission, evidence, approval, version and provider, so that stale authority cannot act.
14. As a customer-service worker, I want customer updates drafted separately from sending, so that sensitive communication is reviewed.
15. As a compliance user, I want a scoped evidence pack, so that I can reconstruct sources, versions, human outcomes and execution.
16. As an interrupted worker, I want committed work preserved and current authority/evidence rechecked on resume, so that I recover safely.
17. As an affected claimant, I want evidence or process errors acknowledged, reviewed, corrected and propagated, so that I have recourse.
18. As an insurer decision owner, I want AI prohibited from final claim, settlement, payment, fraud and complaint decisions, so that accountability remains human.
19. As a reviewer, I want sources, uncertainty, missing evidence and model/rule/workflow versions visible, so that I can calibrate reliance.
20. As an operations owner, I want reviewer workload measured, so that human oversight does not become rubber-stamping.
21. As a security owner, I want document/email/model instructions treated as untrusted data, so that prompt injection cannot grant authority.
22. As an auditor, I want immutable output and workflow lineage, so that historical work remains reconstructible after change.
23. As a responsible-AI owner, I want an impact assessment and stop conditions, so that harmful automation cannot advance by default.
24. As an incident owner, I want pause, affected-case tracing, correction and controlled restart, so that systematic errors are contained.
25. As an architect, I want every important fact to have one authoritative owner and recovery rule, so that data meaning is unambiguous.
26. As a privacy owner, I want every store/message/export scoped and minimized, so that personal data is not copied without purpose.
27. As a claims worker, I want claim, case, evidence, task, approval, execution and payment meanings separated, so that status never implies a decision falsely.
28. As a platform operator, I want idempotency and reconciliation around retries, so that timeouts never create duplicate external effects.
29. As a records/privacy owner, I want approved retention, legal-hold, correction and deletion propagation, so that lifecycle duties cover derived stores.
30. As a security tester, I want cross-tenant enumeration and spoofed authority tests, so that hidden data cannot be inferred.
31. As a customer-facing worker, I want internal notes, reserves, fraud signals and provider traces excluded, so that communications are safe.
32. As a keyboard user, I want every critical task operable without a pointer, so that custom controls do not exclude me.
33. As a low-vision user, I want text zoom and 320 CSS-pixel reflow without lost actions, so that dense work remains usable.
34. As a screen-reader user, I want status and error changes announced without disruptive focus jumps, so that I understand system progress.
35. As a product owner, I want critical tasks to meet measurable completion and recovery targets with representative users, so that “usable” is evidence-based.
36. As a multilingual document reviewer, I want original language preserved with translation provenance, so that a derived translation never replaces evidence.
37. As a reviewer of an unsupported language, I want an honest limitation and human fallback, so that IIP does not fabricate extraction.
38. As a communications owner, I want templates approved by language, purpose, channel and legal basis, so that sends match insurer policy.
39. As a user on poor connectivity, I want critical metadata and pending/retry state to remain usable, so that interruption does not become false success.
40. As an operator, I want queue retries bounded and poison work dead-lettered with replay authority, so that outages remain recoverable.
41. As a performance owner, I want representative p95/p99 and web-vitals gates preserved after failures, so that targets cannot be relaxed silently.
42. As a recovery owner, I want measured RPO/RTO restore evidence, so that backups are more than configuration claims.
43. As a support owner, I want each dependency to show health, owner, alert and runbook, so that failures have an actionable response.
44. As a release manager, I want demo, UAT, pilot and production-candidate gates separated, so that evidence cannot be promoted beyond its environment.
45. As a stakeholder, I want every requirement linked through design, contract, code, test and release evidence, so that gaps remain visible.
46. As a stakeholder approver, I want to approve an exact version with conditions and expiry, so that scope decisions are formal and reviewable.
47. As an implementation agent, I want explicit gaps and blockers rather than blank cells, so that I know what can be progressed safely.
48. As a delivery owner, I want target-insurer decisions to block only their applicable gate while safe source work continues, so that progress is honest.
49. As a customer, I want to submit WhatsApp messages and documents into the correct insurer case workflow, so that I can use the required operating channel without logging into the staff app.
50. As a customer-service worker, I want a case-linked WhatsApp thread and human-reviewed outbound response, so that context, consent and language remain controlled.
51. As an operator, I want WhatsApp acknowledgements, delivery, failures and unknown outcomes separated and reconciled, so that IIP never falsely claims a customer was contacted.

## Implementation Decisions

- Maintain a single atomic Release 1 baseline and use other artifacts as governed projections.
- Release 1 is the staff evidence-review and approval-preparation loop; broad modules are excluded unless required by that loop or an invariant.
- Use authenticated HTTP/browser behavior as the primary test seam, domain tests for invariant state/graph policy, and provider/worker contracts for failure/retry.
- Current runtime routes and generated OpenAPI outrank historical endpoint examples.
- Keep claim, case, evidence, field review, completeness, task, approval, communication, execution, provider and payment state domains distinct.
- Preserve source evidence separately from derived artifacts; every correction invalidates affected downstream work.
- Human approval authorizes only an exact current action/evidence/version; it is separate from execution.
- Protected runtimes fail closed and never simulate provider or business success.
- Preserve original multilingual content and explicit language/translation provenance; unsupported inputs route to human review.
- Use WCAG 2.2 AA and measurable keyboard, screen-reader, reflow, usability, latency, low-bandwidth, retry and recovery gates.
- Treat WhatsApp as required for Release 1 while keeping its provider/business account, consent/legal basis, templates, languages, sender identity, callback policy and live credentials conditional on named approval.
- Treat other channels, client deployment, retention, roles, approval rules, load and support as conditional values that require named approval.
- Record six-function stakeholder approval over exact baseline/scope/acceptance versions before scope freeze.

## Testing Decisions

- Test external behavior, invariant outcomes and safe failure—not private helper implementation.
- Extend existing API tests for membership, tenant isolation, invalid input, state conflict, approvals, correction propagation, audit and export.
- Extend domain tests for transition legality, human-review graph paths, immutable lineage and descendant invalidation.
- Extend worker/provider tests for duplicate delivery, timeout, unknown outcome, bounded retry, DLQ, replay, `Not configured` and human stop.
- Test the critical browser journey from queue through evidence, correction, approval and export across loading, empty, error, stale, unauthorized, interrupted and low-bandwidth states.
- Combine automated accessibility checks with manual keyboard, supported screen reader, 200% text and 400%/320 CSS-pixel reflow evidence.
- Run representative moderated tasks for completion, consequential error, evidence comprehension and recovery; do not infer usability from visual snapshots.
- Run performance near the deployment region with reported p50/p95/p99, load, payload, cache/dependency state and errors; do not relax targets after failure without approval.
- Rehearse restore, migration failure, dependency outage, queue poison, algorithm kill/correction and rollback before production-candidate status.

## Out of Scope

- Autonomous consequential insurance decisions.
- Claim lines and modules explicitly deferred by the scope register.
- Full offline operation, mandatory additional customer channels, mandatory Marketplace deployment, or core-system replacement. WhatsApp remains required for Release 1 intake and reviewed follow-up, without implying every product workflow is channel-first.
- Claiming participant validation, legal approval, paid-pilot commitment, live-provider readiness, UAT or production readiness from documents alone.
- Implementing client-specific defaults before the named client authority decides them.

## Further Notes

- Agents may close current-code mapping, test, documentation and non-client-specific implementation gaps under normal repository governance.
- Human-owned target-insurer, legal/privacy, provider capacity, live Azure, UAT and formal approval actions remain blocked until supplied.
- Current RF-10 evidence remains NO-GO; this specification does not change that verdict.
