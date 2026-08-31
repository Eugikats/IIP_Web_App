# IIP Release 1 Product and Algorithmic Risk Register

| Field | Value |
|---|---|
| Version | 2.2 |
| Date | 2026-08-10 |
| Status | Active proposed controls; named ownership and residual-risk acceptance pending |

## Scale

Probability: Low/Medium/High. Impact: Low/Medium/High/Critical. Any Critical impact or High probability requires a release-gate control and named owner; no numerical score can override legal, human-authority, privacy or tenant-isolation obligations.

| ID | Risk/harm | Probability | Impact | Prevent/detect/respond | Requirement/gate | Owner/status |
|---|---|---:|---:|---|---|---|
| R-001 | Scope expands into disconnected modules before the evidence-review loop is proven. | High | High | Scope register, P0 gap-first sequencing, change control, kill criteria | R1-BUS-001; G0 | Product — pending name |
| R-002 | AI/rules influence or complete a consequential claim outcome without meaningful human authority. | Medium | Critical | Guard + graph/worker enforcement, exact approvals, wrong-output/workload tests, recourse, kill switch | R1-BUS-002; R1-AI-001–007 | Insurance/RAI — pending names |
| R-003 | Cross-tenant or stale scope leaks evidence or contaminates a decision. | Low | Critical | Server membership, scoped stores/messages/exports, re-resolution, enumeration tests | R1-DAT-002; R1-SEC-001–002 | Security — pending name |
| R-004 | Corrected evidence leaves stale summaries, tasks or proposals executable. | Medium | Critical | Provenance/dependency lineage, invalidation and re-evaluation, regression tests | R1-FUN-009; AC-COR-01 | Architecture/Engineering — mapped |
| R-005 | Unsupported output or automation bias causes harmful follow-up or review. | Medium | High | Evidence/limits/version display, severe-error tests, human correction, override/workload monitoring | R1-AI-002–003; AIA | AI Quality/Operations — pending names |
| R-006 | Language, script, scan quality, disability, device or connectivity creates unequal errors/delay. | High | High | Original-source preservation, strata/intersection evaluation, accessible/low-bandwidth recovery, human fallback | R1-A11Y-*; R1-L10N-*; R1-RES-001–002 | Product/Design/Quality — pending names |
| R-007 | Missing/incorrect tenant document rule creates false gaps or hides necessary evidence. | High | High | Versioned approved rules, no invented checklist, exception route, rule tests | R1-FUN-008; OQ-003 | Insurance/Client Ops — blocked |
| R-008 | Provider timeout/retry duplicates sends/write-backs or falsely reports success. | Medium | Critical | Stable idempotency, pending/reconcile state, bounded retry, DLQ, provider confirmation | R1-DAT-006; R1-RES-004 | Architecture/Operations — conditional |
| R-009 | Protected runtime activates mock/fallback, seeded data or DevelopmentAuth. | Medium | Critical | Startup validation, binary/source/config scans, fail-closed readiness, deployed smoke | R1-SEC-004; AC-SIM-01 | Engineering/Security — pending environment proof |
| R-010 | Logs, prompts, exports or customer messages disclose sensitive/internal information. | Medium | Critical | Minimization, safe metadata/references, customer-safe filters, adversarial sampling | R1-PRI-001–004 | Privacy/Security — pending names |
| R-011 | Retention/cross-border/support access is unlawful or unacceptable to buyer. | Medium | Critical | Exact deployment/data record, legal/client approval, deletion/exit rehearsal | R1-PRI-002; OQ-002/007/008/013 | Legal/Privacy — blocked |
| R-012 | Review queues make human oversight nominal or create unsafe backlog. | High | High | Measure volume/time/bulk approval/disagreement, staffing/SLA, throttle/pause | R1-AI-003; AIA | Operations/Insurance — evidence absent |
| R-013 | Performance/low bandwidth prevents safe timely work or encourages workarounds. | High | High | Preserve targets, near-region load, throttled/interruption tests, field telemetry | R1-PERF-*; R1-RES-* | Architecture/Product — current p95 fail |
| R-014 | Backup/restore/migration failure loses evidence or breaks lineage. | Medium | Critical | Versioned migrations, backup/restore, RPO/RTO rehearsal, repair/rebuild | R1-RES-003; R1-OPS-003 | Operations/Architecture — blocked |
| R-015 | Requirements appear complete while practitioner/buyer/configuration evidence is absent. | High | High | Status labels, evidence IDs, approval record, gate separation | R1-BUS-005; R1-REL-* | Product/Delivery — controls drafted |
| R-016 | Existing broad API/design documents reintroduce stale routes, states or scope. | High | High | Current-code/API map authority notes, RTM, conflict log, no legacy fallback | R1-DAT-001/004; G0/G1 | Architecture/Product — active reconciliation |
| R-017 | Prompt injection or untrusted content obtains tools/data authority. | Medium | Critical | Treat content as data, allow-list, permission/approval recheck, incident handling | R1-AI-004/007 | Security/AI Quality — evidence incomplete |
| R-018 | No qualified buyer/pilot means custom work cannot validate market or ROI. | High | High | Commitment ladder, paid-pilot gate, baseline/finance attribution | R1-BUS-004; OQ-001/010/012/013 | Commercial/Product — blocked |
| R-019 | WhatsApp spoofing, wrong tenant/case correlation, unsafe outbound content or ambiguous provider status harms customers or exposes data. | Medium | Critical | Verify webhooks, map business number server-side, deduplicate, manual-review unmatched content, human-reviewed sends, consent/template controls, explicit delivery states and reconciliation | R1-WA-001–004; AC-WA-01 | Security/Privacy/Operations/Architecture — source/local controls complete; insurer policy, protected-staging provider, reconciliation and UAT proof blocked |
| R-020 | Assessor impersonation, wrong/excessive disclosure, malicious attachment, forwarding/stale access, false acknowledgement, or cross-tenant provider/match failure exposes evidence or contaminates review. | Medium | Critical | Approved assessor directory/appointment, exact minimum-pack preflight, dedicated permission, quarantine/malware/parser controls, no bearer evidence links, explicit non-revocation, tenant-scoped provider/match/reconciliation, exact-state audit and negative matrix | R1-SEC-006–009; AC-ASR-01; `THR-R1-ASR-01`–`08` | Security/Privacy/Claims Operations/Architecture — blocked; named owners, implementation, insurer policy, protected evidence and residual forwarding-risk acceptance absent |
| R-021 | Opaque or excessive assessor disclosure, inaccessible notice, unjustified report authority/feedback reuse, or ineffective contestability creates unequal evidence burden, privacy loss, delay, and an adverse claim path that affected people cannot understand or correct. | Medium | Critical | Purpose/item-level minimum-disclosure manifest; accessible claimant notice; contracted external-professional boundary; `Contested` reliance pause and independent reconsideration; no unapproved training/scoring reuse; procedural-parity and burden monitoring | R1-FUN-020; R1-AI-008; R1-PRI-005–007; AC-ETH-01; assessor ethical review | Privacy/Claims Operations/Complaints/Responsible AI — blocked; policy, implementation, claimant/practitioner validation, monitoring and residual-risk approval absent |

## Stop conditions

Stop or limit Release 1 if tenant isolation, human authority, provenance, recourse, correction propagation, representative testing or legal data authority cannot be made acceptable; if reviewer workload makes oversight ceremonial; if a systematic severe error cannot be measured/repaired; or if a simpler safer process provides comparable value at lower total cost and disruption.

## Review cadence

Review at every research synthesis, requirement/scope change, model/provider/rule/workflow version, security/privacy finding, protected deployment, UAT cycle, performance/recovery exercise, incident, pilot midpoint and go/no-go decision. Every High/Critical residual risk needs a named owner, decision, conditions and expiry.
