# IIP Release 1 API and Data Contract Map

| Field | Value |
|---|---|
| Version | 0.12 |
| Date | 2026-08-10 |
| Status | Current-code reconciliation; contract approval pending |
| Authority | Runtime route mappings/OpenAPI, Domain/Application contracts, migrations, tests |

## Contract rule

This map is the Release 1 view. `Program.cs` route mappings and generated OpenAPI are implementation truth. The older broad API specification is a design catalogue; any route not mapped in current code is not an implemented contract.

All `/api/v1` operations require server-resolved membership/permission unless a route is explicitly public for onboarding/webhook/health purposes. Browser headers, body/query tenant IDs, subscription IDs, tokens, groups, and app roles do not grant business authority.

## Release 1 route groups

| Use | Current route(s) | Requirement | Contract state |
|---|---|---|---|
| Runtime identity | `GET /api/v1/me` | R1-FUN-001, R1-SEC-001 | Implemented; protected evidence still required per environment |
| Permission-filtered global search | `GET /api/v1/search?q={query}&limit={1..50}` | R1-FUN-018, R1-DAT-002, R1-PRI-001, R1-SEC-001 | Implemented from authoritative repositories; case assignment and object permissions filter before matching/counting; protected multi-role proof pending |
| Recent Command Center conversations | `POST /api/v1/command-center/query`; `GET /api/v1/command-center/conversations`; `GET/PATCH /api/v1/command-center/conversations/{id}`; archive/restore subroutes | R1-FUN-016, R1-DAT-002, R1-PRI-001, R1-SEC-001 | Owner/tenant/workspace-scoped SQL persistence, current permission checks, historical labeling and optimistic conflict contract implemented locally; policy, advanced findability, change comparison and protected validation pending |
| Universal My Work | `GET /api/v1/my-work?ownership={mine|unassigned|all}&sourceType={type}&state={state}&limit={1..100}` | R1-FUN-003, R1-DAT-002, R1-SEC-001 | Request-time projection over source-owned workflow tasks, approvals, document reviews, communication reviews and agent work items; source states/actions are not merged; protected multi-role, scale and practitioner proof pending |
| Case intake/list/detail | `POST/GET /api/v1/cases`; `GET /api/v1/cases/{caseId}` | R1-FUN-002–004 | Implemented |
| Assignment/escalation | `POST /api/v1/cases/{caseId}/assign`; `/escalate` | R1-FUN-003 | Implemented |
| Claims read views | `GET /api/v1/claims`; `/attention`; `/daily-summary`; `/{claimId}`; `/{claimId}/draft-context` | R1-FUN-003–004 | Implemented read models; contract drift from legacy spec must not be copied |
| Documents | `POST/GET /api/v1/cases/{caseId}/documents`; content, reclassify, approve-extraction, correct-field | R1-FUN-005–009 | Implemented; real provider activation conditional |
| Assessor handoff | No approved dedicated route. DEC-018 selects controlled manual export/import as baseline and Graph email as optional tenant-configured transport; current document intake is only a possible implementation seam, not the approved referral/report contract | R1-FUN-019 | Domain/API/permission/audit contracts remain gaps; external portal and insurer-system integration/write-back deferred; no live external-access, delivery, acknowledgement, receipt or write-back claimed |
| Completeness | `GET /api/v1/cases/{caseId}/document-checklist` | R1-FUN-008 | Implemented rules; target-insurer list unapproved |
| Timeline/workflow | case timeline, recommended-actions, workflow evaluate; workflow task list/detail/complete/assign | R1-FUN-003–013 | Implemented surfaces; current state contracts govern |
| Approvals | approval list/detail/create; approve/reject/override/escalate/execute-next-step | R1-FUN-011–013, R1-AI-001 | Implemented; every consequential path must retain invariant tests |
| Customer updates | case draft/list; update approve/reject/send | R1-FUN-014 | Implemented; provider/template/channel activation conditional |
| WhatsApp inbound | `GET /api/v1/integrations/whatsapp/health`; `GET/POST /api/v1/integrations/whatsapp/webhook` | R1-WA-001 | Signed text/media retention and server mapping source-complete; protected-staging provider proof pending |
| WhatsApp staff thread | `GET /api/v1/channels/whatsapp/threads/{threadId}`; Channels overview | R1-WA-002 | Tenant-scoped durable thread and UI source-complete; multi-role staging/UAT pending |
| WhatsApp outbound/status | `POST /api/v1/channels/whatsapp/threads/{threadId}/messages`; signed status events on webhook | R1-WA-003–004 | Reviewed/idempotent Meta attempt and reconciliation source-complete; insurer template/policy/live callback evidence pending |
| Review requests | case review-request list/acknowledge/resolve; client request-review | R1-FUN-017 | Implemented; insurer complaint/appeal process remains external policy |
| Audit/evidence | audit events; compliance audit-events/posture/case audit-pack/export | R1-FUN-015, R1-PRI-004 | Implemented; tenant and safe-payload tests required |
| Provider/capability state | provider-status, system capabilities, admin provider modes/health, health endpoints | R1-OPS-002, R1-SEC-004 | Implemented. Capability booleans are true only for live providers; `NotConfigured`/`Disabled` stay inactive. Protected runtimes reject Mock/InMemory/placeholder modes at startup. |
| Knowledge/grounding | knowledge document upload/list/get/approve/archive/reindex; policy question | R1-FUN-010, R1-AI-002 | Implemented surfaces; Release 1 activation conditional on approved sources/provider |
| Workflow definitions/runs/work items | agent-workflow definitions/versions/publish/runs/nodes/work-items/decision | R1-AI-001–007, R1-OPS-003 | Implemented control plane; Release 1 allow-list and worker evidence apply |

Client-portal, sales, customer-360, distribution, policies, fraud, underwriting, billing, broad communications, and onboarding routes may exist but are not required Release 1 scope unless separately approved. Existence is not activation authority.

## Data ownership

| Fact/data | Authoritative owner | Write path | Derived/read copies | Consistency and recovery | Retention/deletion |
|---|---|---|---|---|---|
| Tenant/workspace membership and permission | IIP membership/permission store | Authorized administration/onboarding | Request-scoped `TenantContext`, UI navigation | Re-resolve per request/background action; fail closed | Client/IIP access policy; auditable joiner/mover/leaver |
| Case operational record | IIP Cases context | Case Application use cases | Claims queue/detail/metrics | Transactional write; optimistic conflict where relevant; audit | Client-approved claim/case schedule |
| Policy/coverage truth | Insurer core system or approved imported snapshot | Adapter/import only | Case context/read model | Snapshot freshness/source visible; no IIP invention | Insurer policy + contract/legal requirements |
| Source document bytes | Approved Blob container scoped by tenant/workspace/case | Document storage adapter after validation | Authorized content stream only | Durable reference; upload failure is not metadata success | Approved evidence schedule/legal hold/deletion process |
| Document metadata/classification/extraction | IIP Documents context; extraction is derived | Document use cases/provider adapter/reviewer | Case document views/checklist/search | Source/version linked; correction invalidates downstream | Follows source/derived lifecycle and audit exception |
| Evidence eligibility/lineage | IIP evidence/review policy over authoritative sources; never the model alone | Reviewer decisions and current approved deterministic rules | Grounded summaries, completeness, approvals, export | Current/superseded/historical/unverified/contradicted meanings retain source/version/freshness; ineligible evidence is excluded from execution | Reconstruct from source/audit; retention/access policy remains conditional |
| Completeness result | Derived from approved rule version + current evidence | Completeness service | Queue/case/proposal | Rebuild after evidence/rule change; not claim truth | Derived-data lifecycle |
| Workflow definition/version | Workflow control plane | Draft/publish under authority | Pinned run definition | Published version immutable; run never re-resolves to newer version | Governance retention |
| Workflow run/node/work item | Workflow control plane | Authorized worker/use cases | Queues/debugger/telemetry | Idempotent message handling; bounded retry/DLQ/replay | Operational/audit schedule |
| Proposed action/summary | Derived artifact | Application/approved provider | Review/approval surfaces | Current evidence/version; stale blocks execution | Derived-artifact schedule |
| Approval/human decision | Approval context | Authorized human decision use case | Case/work queues/audit | Exact action/evidence/version; first valid decision semantics per policy | Decision/audit retention |
| Universal My Work projection | No independent write owner; derived at request time from source-owned records | None | `/api/v1/my-work` and `/app/work-items` | Rebuilt on each request; source permission/state rechecked on handoff; no dual write or projection table | No independent retention; safe read audit follows audit policy |
| External communication/execution outcome | Communication/integration history; target system confirms external truth | Controlled adapter after approval | Case history/reconciliation | Pending/succeeded/failed distinct; reconcile before retry | Contract/channel/claim schedule |
| WhatsApp message/conversation and delivery | IIP communication history for intent/state; WhatsApp provider confirms external acceptance/delivery/read | Verified webhook or authorized reviewed outbound adapter | Case/channel thread, audit and reconciliation | Provider message ID + stable idempotency/correlation; server-configured scope; duplicate-safe callback; unknown before retry | Approved communication/claim schedule and provider contract |
| Assessor referral and evidence pack | IIP referral context plus Blob/SQL manifest; insurer Claims Operations owns appointment/recipient authority | Proposed authorized referral use case; no route implemented | Case/handoff/reconciliation views | Immutable referral/pack version/hash; same-version export is idempotent; correction creates a new version; due/expiry are insurer-owned | Approved evidence/claim schedule; legal hold/deletion policy pending |
| Assessor transport/acknowledgement | IIP attempt history for intent; named staff owns manual attestation; Graph owns provider acceptance; assessor owns explicit acknowledgement | Proposed manual or configured Graph adapter path; no route implemented | Status region, queue, audit/reconciliation | Export, attestation, Graph acceptance, unknown, acknowledgement and receipt remain distinct; unknown reconciles before retry | Communication/claim schedule; Graph mailbox policy pending |
| Assessor returned report | Original external source in Blob plus IIP SQL association; authorized human owns evidence status | Proposed quarantine/intake/match/review path; current document intake is only a seam | Evidence review, case timeline, revision chain | Dedupe by provider/source identity and hash; ambiguous/late/unsafe enters owned review; revision supersedes without erasure | Approved evidence/claim schedule; retention/legal hold/deletion pending |
| Assessor disclosure manifest and claimant notice | IIP owns the immutable record of proposed/authorized disclosure and notice/exception; insurer owns purpose, basis, necessity taxonomy, recipient and notice policy | Proposed referral preflight/release use case; no route/schema implemented | Case/handoff/privacy review and audit references | Exact question/item/version/hash binding; correction requires new manifest; released bytes must reconcile to authorized manifest | Claim/privacy/communication schedule; external handling terms and legal basis pending |
| Assessor evidence contestability | Insurer Complaints/authorized reviewer owns remedy; IIP owns request, exact-version links, `Contested` currentness and reliance-block state | Proposed mediated review-request and evidence-state transition; generic review-request seam is not assessor contract | Customer Service/Complaints queue, evidence review, decision guard and audit | Stable request identity; preserve originals; material contest blocks new reliance until authorized reconsideration; correction invalidates dependants | Complaint/review/legal schedule; response target, independent-review and remedy policy pending |
| Review request | Contestability context | Authorized client/staff request and reviewer resolution | Case/review queue/audit | Correction propagates to dependent state | Complaint/review/legal schedule |
| Audit event | Audit context | Application audit writer | Compliance views/export | Append-oriented, safe metadata, correlation and version | Approved audit schedule; legal hold/subject rights reviewed |
| Global search | Authoritative case/document/workflow/approval repositories for current baseline; any future index is derived | `GlobalSearchService`; future projection/index pipeline only after measured need | Portal `/app/search` | Permission filter precedes matching/counting; raw query excluded from audit; no unrestricted text or sensitive-field indexing | Current baseline has no second index; future derived lifecycle and privacy propagation remain required |
| Command Center conversation history | IIP Command Center conversation repository; never evidence or permission | Successful `POST /api/v1/command-center/query`; owner rename/archive/restore | Sidebar recent list and `/app/conversations*` | Scope and owner filter before payload return; next turn reauthorizes and reloads live query data; integer concurrency token; safe lifecycle/view audit metadata | No automatic deletion; retention, legal hold, correction, deletion request, export and searchable transcript policy require approval |
| Cache/metrics | Derived subsystem | Projection pipeline | Portal/reports | Freshness visible; rebuild/repair; never authority | Derived lifecycle and privacy propagation |

## Write contract checklist

Every important write defines: authoritative owner; validated tenant/workspace; actor/permission; expected version/state; input validation; transaction/durability point; audit; idempotency; external-side-effect ordering; retry/reconciliation; correction/invalidation; and user/operator recovery.

## Failure contract

| Failure | Required outcome |
|---|---|
| Missing/invalid authority | Fail closed without cross-tenant existence leak |
| Validation/unsafe content | Reject or quarantine with safe reason; no downstream processing |
| Provider absent | `Not configured`; no simulation or fallback |
| Provider timeout/unknown result | Pending/reconciliation; do not repeat blindly |
| Duplicate/replay | Deterministic no-op/link or explicit conflict under stable idempotency identity |
| Stale version/concurrent write | Conflict; preserve committed state; reload/reapply intentionally |
| Evidence correction | Preserve before/after; invalidate dependent derived work; re-evaluate |
| Queue poison/retry exhaustion | Dead-letter/manual intervention with owner, diagnostics, and authorized replay |
| Missing/unverified/low-confidence/conflicting/stale/superseded evidence | Preserve provenance and route according to `FLW-R1-EVIDENCE-01`; block dependent consequence until eligible current review exists |
| Unauthorized/unsupported/duplicate/corrupt/unsafe evidence | Deny, historical-only, link/no-op, reject, quarantine, replacement, or qualified review as applicable; never use silently |
| Assessor identity/recipient unverified, stale, changed or spoofed | Block release/match; preserve draft/quarantine; require insurer-owned appointment/recipient reverification; sender/reply/token is not authority |
| External attachment validation/malware/parser control absent or unavailable | Hold in tenant-scoped quarantine; no extraction, preview, search, AI, export or current evidence; never infer clean from provider metadata |
| Forwarded/stale/replayed assessor link/token | No Release 1 bearer evidence link/SAS; deny access/match; expiry does not claim remote attachment revocation |
| Assessor/provider scope mismatch | Fail closed without existence leak; no cross-tenant mailbox/message/attachment/case/blob/referral lookup, replay or reconciliation |
| Missing/excessive disclosure purpose, item necessity, claimant notice/approved exception, or external handling terms | Preserve draft; block release; identify the owning Privacy/Claims/Legal decision without exposing restricted content; never infer consent or necessity from prior practice |
| Material claimant dispute of assessor disclosure/report/reliance | Acknowledge and assign; preserve exact versions and context; set `Contested`; block new reliance/execution until authorized reconsideration, reason and remedy/correction propagation |
| Migration/release failure | Roll back or forward-recover using recorded version and runbook |

## Test seams

- Authenticated HTTP contract tests: status, safe body, authorization, tenant isolation, validation, state conflict, human gate, audit.
- Domain invariant tests: claim/workflow transitions, approval guard, immutable lineage, descendant invalidation.
- Provider/worker contract tests: timeout, duplicate, retry, DLQ, unknown outcome, `Not configured`, human stop.
- Critical browser tests: queue → evidence → correction → approval → export, including keyboard, recovery, stale evidence, and restricted states.

The client-facing reconciliation for every Release 1 interaction is maintained in `../03-design-architecture/iip_release_1_interaction_accessibility_and_runtime_parity.md`. That artifact may identify client/API/provider/concurrency/audit/idempotency gaps, but it does not alter this map's current runtime contracts.

## Open contract decisions

Target-insurer document rules, retention, roles, approval expiry/two-person controls, assessor recipient/purpose/question/item-necessity/disclosure/due/expiry/manual-channel policy, claimant notice/basis/exception/accommodation/contestability policy, external-professional delegate/onward-sharing/AI/subprocessor/storage/retention/deletion/incident/audit terms, optional Graph mailbox/consent/RBAC/renewal/reconciliation, WhatsApp provider/business account/consent/templates/languages/sender/callback policy, other provider retry/DLQ policies, deployment/region/support access, and pilot load remain conditional. External assessor portal and assessor-path core integration/write-back are deferred under DEC-018. These details block protected activation and never authorize invented defaults.

Current permission reconciliation also leaves `PERM-GAP-01`–`05`: Compliance has settlement-approval/override permissions; Claims Officer/Customer Service expose broad workflow capabilities; assignment authority is source-specific; `UploadDocument` combines upload with extraction-review actions; and no assessor referral/disclosure/export-attestation/acknowledgement/reissue/match/revision permission contract exists. These are current contracts, not approved least-privilege conclusions. No route or permission changes are made by the journey specifications.
