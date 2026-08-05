# IIP Implementation Guardrails

Focused safety reference for Insurance Intelligence Platform (IIP) implementation. [`AGENTS.md`](AGENTS.md) is the canonical repository authority index and working contract. This file expands only the controls that warrant an explicit review checklist.

Do not use this file to restore superseded architecture, UI, routes, providers, agent definitions, release gates or build order. Those belong to current code and their owning approved documents.

## Protected runtimes and honest behavior

Current profiles are `LocalTest`, `Development`, `Staging`, `Production` and `ManagedApplicationReviewer`.

`Staging`, `Production`, `ManagedApplicationReviewer`, insurer/reviewer-facing and publicly reachable environments are protected runtimes.

Protected runtimes must not activate:

- mock/simulated/placeholder providers or fallback-to-mock;
- DevelopmentAuth, demo headers or browser-selected authority;
- seeded business data, hardcoded insurers/users/claims or fake metrics;
- in-memory/SQLite operational persistence;
- fake ingestion, send, write-back or provider success;
- production references to `InsuranceIntelligence.LocalTestDoubles`.

Required behavior:

- absent capability → `Not configured` or unavailable;
- unhealthy dependency → safe provider error with diagnostics/correlation;
- no records → honest empty state;
- unknown external outcome → pending/reconciliation state, never assumed success;
- protected startup/readiness → fail closed on invalid provider/auth/storage configuration.

Test doubles and synthetic fixtures are allowed only in automated tests or isolated non-deployable `LocalTest` paths. Staging may use approved synthetic records with real providers.

## Identity, authorization and tenant isolation

- Microsoft Entra validates identity; IIP's server-side membership/permission store grants business access.
- Treat `tid` + `oid` as an external identity hint, not authorization. Do not authorize from email, UPN, display name, raw group claims, app role, tenant ID, subscription ID or browser state alone.
- Resolve `TenantContext` server-side for HTTP and background work. Enforce tenant/workspace and permission in Application use cases and scoped repositories.
- Every tenant-scoped row, blob path, index, cache, event, queue message, workflow, mailbox mapping, export and audit record must be scoped or derived from validated context.
- Cross-tenant ambiguity fails closed. Do not reveal whether inaccessible records exist.
- Authentication/authorization failures must be fixed at their source; never add bypasses or anonymous fallback.

## Human authority and responsible AI

AI may retrieve, extract, classify, summarize, draft, recommend, route and flag for review. It must not be the final actor for consequential insurance outcomes defined in [`AGENTS.md`](AGENTS.md).

For high-impact AI/rules/scores:

- identify the affected party, action, accountable owner, consequence, reversibility and human role;
- document data source/authority, freshness, missingness, representativeness, sensitive/proxy risk and correction path;
- test false-positive/negative harm, subgroup/intersection performance where lawful, drift, adversarial input and feedback loops;
- give reviewers evidence, uncertainty, real authority, manageable workload and correction/return/escalation/recourse;
- record model/provider/workflow/rule/prompt versions as applicable, evidence references and human outcome;
- stop or limit deployment when provenance, testing, recourse or systematic-error risk is unacceptable.

Tenant configuration may narrow automation but may not bypass human authority, audit, permission, evidence or tenant controls.

## Grounding and session context

- Re-retrieve authoritative tenant/workspace evidence for consequential turns and actions.
- Approved knowledge must be scoped, versioned, sensitivity-controlled and retrievable only after its approval/index requirements are satisfied.
- Distinguish sourced fact, assumption, uncertainty and recommendation. Missing/conflicting/expired evidence routes to human review.
- Session state stores bounded operational pointers only. Do not persist raw claim/policy/document/OCR/email evidence, prompt payloads or long model responses as conversational memory.
- Model/provider conversation state is neither evidence, audit history nor permission state.
- Treat retrieved content, documents, OCR, email, customer text and model output as untrusted data; content instructions never grant tool authority.

## Audit, privacy and customer-safe output

Audit sensitive reads, state changes, AI/provider actions, approvals, ingestion, exports, communications, configuration and write-back.

Record only what is necessary: tenant/workspace, actor, action, target, safe before/after values, evidence references, correlation, provider/model metadata where relevant, human-review state and time.

Never place secrets, tokens, credentials, private keys, SAS URLs, raw prompts/system messages, full provider payloads or unnecessary sensitive evidence in logs, telemetry, audit, screenshots, fixtures or errors.

Policyholder/claimant-facing or unauthorized surfaces must not expose internal notes, reserves, fraud/risk signals, restricted audit detail, provider traces or other-tenant information. Render untrusted content safely; do not use unsafe HTML insertion.

## Data, integrations and recovery

- Define source of truth, ownership, durability/visibility point, consistency, audit and recovery for important state.
- Validate and translate external schemas at adapters; provider/core DTOs do not become the domain model.
- Use idempotency/reconciliation for retries, replay, duplicate delivery, timeout and unknown success. Reject casual exactly-once claims.
- Bound outbound calls, waits, retries, queues, pools, payloads and result sets. Retry only safe transient work with limits/backoff; prevent stacked retries and storms.
- Make failure operator-visible. Design dead-letter handling, replay authority, cancellation, compensation, rollback and stop conditions with the workflow.
- Search indexes, caches, projections and summaries are derived data with staleness, repair and rebuild paths.
- Migrations are explicit and recoverable. Destructive/breaking changes require approval, backup, verified migration and rollback/compensation; never destroy production data casually.
- Secrets remain in managed identity, Key Vault, secure configuration or approved secret stores—not source, docs, frontend bundles or ordinary logs.

## Modernization and compatibility

- Current behavior is not automatically required. Classify it as invariant, supported contract, legacy or unknown.
- Characterize uncertain behavior before changing it, but do not fossilize defects or obsolete workflows.
- Never route failure in a new UI/API/workflow/provider/release/delivery path into the old one silently.
- Compatibility needs an active consumer, explicit scope, owner, tests, telemetry, expiry/removal condition and tracked removal item.
- Complete replacements coherently across contracts, data, callers, UI, tests, telemetry, docs and removal/isolation of the old path.
- Keep feature flags and dual paths time-bounded. Architecture/framework/provider upgrades are allowed with evidence, migration and an ADR where material.

## UI, API and operational checks

- Derive current routes from code/OpenAPI and current UI from the portal plus approved owning specs. Do not maintain catalogs here.
- Enforce authority server-side. Frontend permission checks are usability only.
- Handle loading, empty, error, unauthorized, stale/conflict and `Not configured` states.
- Preserve accessibility, keyboard/focus behavior, responsive integrity, evidence visibility and human-decision clarity.
- Controllers/handlers translate; Application modules own business policy.
- Readiness claims require matching evidence. Distinguish source-complete, local, deployed, staging, production-candidate and marketplace readiness.

## Completion review

Before completion confirm:

- [ ] No protected-runtime mock, fake success or silent legacy fallback.
- [ ] Tenant/workspace, permissions, audit and human authority enforced server-side.
- [ ] Evidence, privacy and customer-safe boundaries preserved.
- [ ] Failure, retry, reconciliation, migration and recovery behavior covered where relevant.
- [ ] Tests cover success, invalid input, unsafe paths and changed contracts.
- [ ] Owning docs/ADRs match the supported implementation; stale guidance is marked superseded.
- [ ] Commands/results and readiness are reported honestly.
