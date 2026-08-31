# IIP WhatsApp Thread, Outbound and Delivery Contract

**Status:** Source-complete and locally verified baseline; not staging-validated or production-ready
**Date:** 2026-08-07
**Owners:** Product, Insurance Operations, Architecture, Security/Privacy, Delivery
**Requirements:** R1-WA-001–004; AC-WA-01; DEC-017

## 1. Boundary and decisions

IIP retains provider-neutral, tenant/workspace-owned WhatsApp messages. The configured business phone number maps to one server-side tenant/workspace and integration actor. Browser role, webhook body, recipient input and provider history are never tenant authority.

The supported implementation adapter is Meta WhatsApp Cloud API. Kapso or another BSP may replace it behind the same ports only after provider approval, data-processing/residency review, signature and event-contract tests, and an explicit migration decision. No live provider account, template or cloud configuration was changed by this work.

An authorized human must review the exact recipient and customer-safe message and record purpose, consent or legal basis, language and template policy. IIP makes one idempotent provider attempt. `ProviderAccepted` means WhatsApp returned a message ID; it is not delivery. Delivery/read/failure truth comes from verified callbacks.

## 2. Canonical object model

| Object | Owner/source of truth | Identity | Required links |
|---|---|---|---|
| WhatsApp thread | IIP projection over retained messages | Opaque SHA-256-derived `wa:{digest}`; contact number is not embedded in routes/audit | Server-mapped scope; optional authorized case until manual link review |
| WhatsApp message | IIP `WhatsAppMessages` store | IIP `messageId`; provider `wamid` when known | Thread, direction, content type, safe body, timestamps, state |
| Outbound attempt | IIP message plus idempotency/approval metadata | Tenant/workspace + idempotency key | Case, verified inbound recipient, reviewer, purpose, basis, language/template |
| Delivery receipt | WhatsApp verified webhook | Provider message ID + event/status | Existing outbound message in same server scope and business number |

The unique tenant/workspace indexes on provider message ID and idempotency key make inbound replay and outbound retry duplicate-safe. Nullable case linkage preserves unmatched inbound work for authorized review without inventing case authority.

## 3. HTTP contracts

| Method/path | Authority | Outcome |
|---|---|---|
| `GET /api/v1/integrations/whatsapp/health` | Integration configuration access | Separate inbound, outbound and server-mapping readiness; missing capability is `Not configured` |
| `GET /api/v1/integrations/whatsapp/webhook` | Meta verification token | Subscription challenge only |
| `POST /api/v1/integrations/whatsapp/webhook` | Valid raw-body `X-Hub-Signature-256`; configured server mapping | Deduplicated inbound text/media retention, existing document processing, delivery reconciliation |
| `GET /api/v1/channels/whatsapp/threads/{threadId}` | Server permission `ViewChannels`; resolved tenant/workspace | Chronological role-safe retained thread |
| `POST /api/v1/channels/whatsapp/threads/{threadId}/messages` | `SendCustomerUpdate` and `ManageCustomerCommunications`; resolved scope | One reviewed outbound attempt; explicit delivery state |

Outbound body requires `caseId`, verified `recipientWaId`, `customerSafeBody`, `purpose`, `consentBasis`, `language`, `humanApproved`, and `idempotencyKey`. `templateName` and named `templateParameters` are supported. Outside the latest verified inbound 24-hour service window, a template is mandatory. The provider remains responsible for confirming that the named/language template is approved.

## 4. State machine and UX language

| State | Meaning | Required operator language/action |
|---|---|---|
| `Received` | Verified inbound retained | “Received” |
| `Queued` | Durable IIP record exists; provider call not yet resolved | “Queued internally” |
| `ProviderAccepted` | Provider returned a message reference | “Accepted by WhatsApp — delivery not confirmed” |
| `Delivered` | Verified delivery callback received | “Delivered” |
| `Read` | Verified read callback received | “Read” |
| `Failed` | Provider rejected or reported failure | “Delivery failed”; show safe reason; require review before a new attempt |
| `Unknown` | Attempt may have reached provider but acknowledgement is missing | “Provider response unknown — reconcile before retrying” |

Late or duplicate callbacks fill delivery/read/failure timestamps but cannot regress `Read` to `Delivered`. Business-number mismatch and unknown provider message IDs are ignored safely. No callback can authorize another tenant or create an outbound attempt.

## 5. Complete user flow

1. Verified inbound webhook resolves the configured tenant/workspace and business number.
2. IIP deduplicates the provider message, retains text/media thread context, and links an explicit case GUID only when that case exists in scope; otherwise it remains manual case-link review.
3. Authorized staff open Channels, select the real WhatsApp thread, and see direction, time and honest delivery wording.
4. Before reply, staff confirm the linked case, recipient, exact customer-safe content, purpose, basis, language and, when required, approved template.
5. Explicit review creates a durable `Queued` attempt before provider I/O.
6. A provider message reference advances the attempt to `ProviderAccepted`; timeout/network ambiguity becomes `Unknown`; provider rejection becomes `Failed`.
7. Signed callbacks advance delivery facts idempotently. Operators reconcile `Unknown` or `Failed` before creating a new idempotency key.
8. Re-entry restores the retained thread and current delivery states; no browser-only success is authoritative.

Alternate/error states: invalid signature `401`; missing provider/mapping `503 Not configured`; unlinked/wrong-recipient thread `404`; missing approval/purpose/basis/language/template `400`; reused key with different content `409`; ambiguous provider acknowledgement `202 Unknown`.

## 6. Safety, ethics and privacy

- AI may prepare a draft but cannot approve or dispatch it. The API records the authenticated human as reviewer at attempt time.
- Internal notes, reserves, fraud/risk signals, prompts and secrets are blocked by the baseline restricted-content check and must also be covered by insurer-approved DLP/content policy before release.
- Audit events omit message bodies and retain identifiers/outcomes only. Application and provider logs must never include access tokens, full message content or unnecessary recipient data.
- Unmatched inbound content is visible only through permissioned tenant/workspace channel access and requires manual case-link review.
- This is an engineering safety baseline, not legal approval of consent, retention, WhatsApp terms, cross-border transfer or customer-notification wording.

## 7. Reliability and recovery

- Persist before provider I/O; tenant/workspace + idempotency key guarantees one logical attempt.
- Never auto-retry an `Unknown` attempt. Reconcile by provider message/history or a verified callback first.
- Webhook signatures cover raw bytes before JSON parsing. Provider message/status events are scoped again by configured business number.
- Unique provider-message indexes provide replay protection; callbacks are monotonic and duplicate-safe.
- Protected runtime has no mock fallback. Health exposes inbound, outbound and mapping readiness separately.
- Required operational follow-up: queue webhook work so acknowledgement remains below the provider timeout, add dead-letter/reconciliation jobs, callback-lag/error metrics and alerts, retention/erasure runbook, backup/restore test, and provider outage playbook.

## 8. Verification and evidence limits

Locally implemented evidence:

- service tests for idempotent outbound, 24-hour template enforcement and monotonic callback reconciliation;
- parser tests for inbound text and delivery events;
- provider adapter tests for text, named-template payloads and safe provider errors;
- SQL migration and tenant-scoped Channels projection;
- solution build and affected test suites.

Still required before closing R1-WA-001–004 or AC-WA-01:

- named insurer approval of provider/BSP, business account/number, templates, languages, consent/legal basis, retention and customer wording;
- protected-staging Meta webhook verification with replay/spoof tests and real inbound text/document journey;
- real outbound template/session-window tests, wrong-recipient/adverse-content review, delivery/read/failed callback evidence and reconciliation drill;
- permissioned multi-role browser/UAT, accessibility and low-bandwidth validation;
- production reliability controls listed above plus security/privacy, architecture, operations and delivery approvals.

## 9. Atomic acceptance checks

- **WA-AC-001:** invalid signature, wrong configured phone number or missing mapping causes no retained message/status mutation.
- **WA-AC-002:** repeated provider inbound ID creates one retained message.
- **WA-AC-003:** unmatched inbound remains visible for authorized manual review without a fabricated case link.
- **WA-AC-004:** thread read returns only resolved tenant/workspace data to `ViewChannels` authority.
- **WA-AC-005:** outbound is blocked without exact human approval, case-linked verified recipient, purpose, basis, language or required template.
- **WA-AC-006:** repeated idempotency key performs one provider call; changed payload with the same key conflicts.
- **WA-AC-007:** provider acceptance is never labeled delivered; timeout/network ambiguity becomes `Unknown`.
- **WA-AC-008:** duplicate/out-of-order callbacks are safe, business-number scoped and never regress `Read`.
- **WA-AC-009:** provider absence stays `Not configured`; protected runtime never simulates a send or receipt.
- **WA-AC-010:** audit records actor/scope/action/reference/state while omitting message body and secrets.
