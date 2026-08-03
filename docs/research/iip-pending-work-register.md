# IIP Pending Work Register

**Purpose:** Durable store of work that was intentionally deferred while shipping gated slices. Items here remain open until closed with evidence. Consult this file before claiming a capability is production-ready, and when starting related work so deferred gates are not forgotten.

**Authority:** Complements `iip-commitment-reconciliation-backlog.md` (commitment delta) and `iip-current-state-capability-map.md` (honesty register). This file tracks **carry-forward engineering/ops prove items** tied to code already landed.

**How to use**
- Add a row when a slice ships with known unfinished follow-ups.
- Update `Status` only with evidence (test run, staging prove, config live).
- Do not delete closed rows — mark `CLOSED` and keep the evidence note.
- When implementing nearby features, scan **Blocks / revisit when** so pending items get closed or explicitly re-deferred.

**Status values:** `OPEN` · `BLOCKED` (needs manual Azure/vendor/legal) · `IN_PROGRESS` · `CLOSED`

---

## Channel adapters (Uganda inclusion) — opened 3 Aug 2026

| ID | Item | Opened by | Why deferred | Revisit when | Owner track | Status |
|---|---|---|---|---|---|---|
| PEND-SMS-01 | Live Africa's Talking sandbox/prod send prove | `feat(sms)` adapters on `harry/sms-africas-talking` | Credentials + reachable AT environment not available in repo automation | Staging SMS config present; or before claiming SMS notifications BUILT | Ops / Gate 3 | **OPEN** / BLOCKED on credentials |
| PEND-SMS-02 | Tenant policy field for transactional auto-send (beyond `AfricasTalking:AutoSendTransactionalSmsEnabled`) | SMS auto-send policy research + `OutboundSmsGuard` | Platform config flag is enough for fail-closed default OFF; tenant-admin policy store not wired | Touching `TenantAgentCapabilityPolicy` or SMS send UX | Application | **OPEN** |
| PEND-SMS-03 | Approved-template + consent/opt-out gates on auto-send path | `docs/research/sms-auto-send-policy.md` | Tiered guard uses `LowRisk` + opt-in only; full policy matrix needs template catalog + opt-out store | Before enabling auto-send in any customer staging | Application / Compliance | **OPEN** |
| PEND-SMS-04 | Ugandan lawyer confirmation of transactional SMS auto-send stance | Research note | Explicitly not legal advice | Before production auto-send ON for any tenant | Legal / founder | **OPEN** / BLOCKED |
| PEND-WA-01 | Live Meta WhatsApp Cloud API webhook + media download prove | `feat(whatsapp)` on `harry/whatsapp-inbound-documents` | No Meta app credentials / public HTTPS callback in local automation | Staging webhook URL + secrets; or before claiming WhatsApp submission BUILT | Ops / Gate 3 | **OPEN** / BLOCKED on credentials |
| PEND-WA-02 | EF multi-number WhatsApp channel mapping (beyond single `WhatsAppOptions` tenant/workspace) | WhatsApp inbound slice | One-insurer-per-Managed-App mapping is correct for default deploy; multi-WABA needs tables | Second phone number or multi-tenant SaaS channel | Infrastructure | **OPEN** |
| PEND-WA-03 | Outbound WhatsApp sending | WhatsApp inbound-only slice | Inbound document path was the T1 commitment; outbound remains Not configured | After inbound live prove; customer asks for WA replies | Infrastructure | **OPEN** |
| PEND-WA-04 | MIME expansion for WhatsApp `image/webp` (and other WA types) | `WhatsAppAttachmentProcessor` allow-list | Kept aligned with portal upload MIME set until business decision | Product confirms webp as claim evidence | Domain / Documents | **OPEN** |
| PEND-WA-05 | Open question D3 — WhatsApp BSP choice (Meta direct vs Twilio vs Infobip) | `docs/06-decisions/open-questions-tracker.md` | Port is provider-agnostic; first adapter is Meta Cloud API | Before production channel contract | Product | **OPEN** |
| PEND-DOC-01 | Formal `IDocumentUploadService` Application service (spec exists; channel path uses storage/processor spine) | WhatsApp/email attachment processors | Existing `IDocumentStorageProvider` + case document path works; full Application service is a larger vertical | Document upload refactor or second channel needing shared upload contract | Application | **OPEN** |
| PEND-UP-01 | Resumable / chunked block upload (TUS or Azure Block Blob multi-part) | T1 low-bandwidth slice | Client compression + progress is enough for month-1; chunked sessions need new API + state | Field agents on very unreliable links still fail mid-upload | Infrastructure | **OPEN** |
| PEND-UP-02 | Service Worker / IndexedDB offline upload queue | T1 low-bandwidth slice | Honest offline message only — no fake queued success | Offline field-agent app track (T2) | Portal | **OPEN** |
| PEND-UP-03 | TIFF in-portal preview | Added `.tif`/`.tiff` to upload allow-list | Preview remains non-previewable without conversion | Document preview conversion step | Portal / Documents | **OPEN** |

---

## Production Release Program cross-cuts (carry-forward)

| ID | Item | Notes | Status |
|---|---|---|---|
| PEND-T0-01 | Live Azure staging with real providers | Blocks honest SMS/WhatsApp/Doc Intelligence prove | **OPEN** / BLOCKED |
| PEND-T0-02 | One real document extraction → human review e2e | Demo-critical | **OPEN** / BLOCKED on PEND-T0-01 |
| PEND-T1-CI | GitHub Actions build/test/lint/infra gates | `.github/workflows/ci.yml`: backend build+test+vuln-scan, portal lint+checks+build, infra bicep compile. **First run green** (run 30807368448 — all 3 jobs ✓). Minor follow-up: actions emit Node-20-deprecation warnings (bump action majors when available). | **CLOSED** |

---

## Change log

| Date | Change |
|---|---|
| 2026-08-03 | Register created. Seeded SMS + WhatsApp deferred prove/policy items from channel adapter slices. |
| 2026-08-03 | Added PEND-UP-01…03 from low-bandwidth upload T1 (compression shipped; resumable/offline-queue deferred). |
| 2026-08-03 | PEND-T1-CI → IN PROGRESS: added `.github/workflows/ci.yml`; matched to real scripts/infra (no `main.bicep`). First-run verification on remote still pending. |
| 2026-08-03 | PEND-T1-CI → CLOSED: first CI run (30807368448) green on all 3 jobs. |
