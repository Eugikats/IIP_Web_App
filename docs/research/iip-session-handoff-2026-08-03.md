# IIP Session Handoff — 3 August 2026

**For:** next coding agent / human continuing Uganda T1 + Production Release work  
**Current branch tip:** `harry/t1-low-bandwidth-upload` @ `80c48d7`  
**Related prior branches / commits still in history:** SMS (`d2c8dc5`, `dbcf96d`), WhatsApp (`8496059`)

---

## Mission context

IIP is executing the **Production Release Program** and the **commitment reconciliation backlog** (UNDP answers as binding target). Near-term engineering has been shipping **Uganda inclusion T1 channel/upload slices** behind config, with honest `Not configured` when credentials are missing. Live Azure staging prove remains the big T0 blocker for claiming BUILT.

Authoritative contracts: `AGENTS.md`, `IIP_IMPLEMENTATION_GUARDRAILS.md`, `CLAUDE.md`.

---

## What shipped this session chain

| Commit | Branch lineage | What |
|---|---|---|
| `d2c8dc5` | `harry/sms-africas-talking` | Real Africa's Talking `ISmsProvider` adapter |
| `dbcf96d` | same | Tiered `OutboundSmsGuard` + `AutoSendTransactionalSmsEnabled` (default OFF) + research note |
| `8496059` | `harry/whatsapp-inbound-documents` | Meta WhatsApp Cloud API inbound webhook → case documents |
| `80c48d7` | `harry/t1-low-bandwidth-upload` | Pending-work register + claim-review photo compress / upload UX + 25 MB / TIFF limits |

---

## Mandatory pending store (do not ignore)

**File:** [`docs/research/iip-pending-work-register.md`](./iip-pending-work-register.md)

Linked from [`iip-commitment-reconciliation-backlog.md`](./iip-commitment-reconciliation-backlog.md).

Open rows that must be revisited when touching related code or before claiming production readiness:

| ID | Summary | Status |
|---|---|---|
| PEND-SMS-01 | Live Africa's Talking send prove | OPEN / BLOCKED (credentials) |
| PEND-SMS-02 | Tenant policy field beyond platform config flag | OPEN |
| PEND-SMS-03 | Template + consent/opt-out gates for auto-send | OPEN |
| PEND-SMS-04 | Lawyer confirmation of transactional auto-send | OPEN / BLOCKED (legal) |
| PEND-WA-01 | Live Meta webhook + media prove | OPEN / BLOCKED (credentials) |
| PEND-WA-02 | EF multi-number WhatsApp mapping | OPEN |
| PEND-WA-03 | Outbound WhatsApp | OPEN |
| PEND-WA-04 | `image/webp` MIME allow-list decision | OPEN |
| PEND-WA-05 | D3 BSP choice (Meta vs Twilio vs Infobip) | OPEN |
| PEND-DOC-01 | Formal `IDocumentUploadService` Application service | OPEN |
| PEND-UP-01 | Resumable / chunked upload | OPEN |
| PEND-UP-02 | Offline upload queue (SW/IndexedDB) | OPEN |
| PEND-UP-03 | TIFF portal preview | OPEN |
| PEND-T0-01 | Live Azure staging real providers | OPEN / BLOCKED |
| PEND-T0-02 | One real extraction → human review e2e | OPEN / BLOCKED on T0-01 |
| PEND-T1-CI | GitHub Actions gates | OPEN |

**Rule for next agent:** when shipping a related slice, either close a row with evidence or explicitly re-defer it in the register changelog.

---

## Runtime / config notes

- SMS: `INSURANCEINTELLIGENCE_SMS_PROVIDER=AfricasTalking` + `AfricasTalking:*` (now bound in `BindOptions`).
- WhatsApp: `INSURANCEINTELLIGENCE_CHANNEL_PROVIDER=WhatsAppCloudApi` + `WhatsApp:*` (PhoneNumberId, AccessToken, AppSecret, VerifyToken, TenantId, WorkspaceId, IntegrationUserId).
- Webhooks: `GET/POST /api/v1/integrations/whatsapp/webhook` (signature required on POST).
- Upload: Claim Review `DocumentUploadWidget` → `POST /api/v1/cases/{caseId}/documents`; client JPEG downscale for images.

---

## Suggested next slices (pick one)

Priority from backlog (T0 still dominates demo honesty; T1 continues if T0 stays blocked):

1. **T0** — Live Azure staging + one Doc Intelligence extraction prove (unblocks SMS/WA live prove too).
2. **T1 CI** — GitHub Actions build/test/lint/infra (`PEND-T1-CI`).
3. **T1 Contestability** — SMS status query + human re-review request (builds on SMS adapter).
4. **T1 Agent-assisted intermediary** — role + simplified workspace (larger).
5. Close a **PEND-*** row that no longer needs credentials (e.g. tenant SMS policy field).

Do **not** claim SMS/WhatsApp BUILT until PEND-SMS-01 / PEND-WA-01 are CLOSED.

---

## Untracked / ignore unless asked

- `IIP_Docs_1-9_Complete.zip`
- `docs/03-design-architecture/iip_end_to_end_user_flow_architecture.md`
- `.agents/`, `skills-lock.json` (local tooling noise)

---

## Validation already run

- SMS provider tests: passed (23).
- WhatsApp + registration tests: passed (12).
- Document upload validation tests: passed (8).
- Portal `tsc --noEmit`: passed for low-bandwidth changes.

---

## Release readiness (evidence-backed)

```text
PRODUCTION-CANDIDATE: NO
MANAGED APPLICATION PACKAGE READY: NO
PARTNER CENTER PRIVATE PLAN READY: NO
```

Reason: no live Azure staging prove; channel adapters are config-gated PARTIAL only.
