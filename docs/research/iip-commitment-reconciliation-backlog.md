# IIP Commitment Reconciliation Backlog

**Created:** 3 August 2026
**Premise:** The UNDP application answers are a **binding specification**. This backlog is the delta between what was promised and what the codebase currently provides (per `iip-current-state-capability-map.md`). Goal: make every commitment **true in the system**.
**Target window:** ~1 month.

**Carry-forward pending prove/follow-ups:** see [`iip-pending-work-register.md`](./iip-pending-work-register.md) — open items deferred from shipped slices (SMS/WhatsApp live prove, auto-send policy depth, etc.). Update that register when closing or adding deferred work; do not treat PARTIAL adapters as BUILT until those rows are CLOSED.

## Status legend
BUILT = real & tested · PARTIAL = scaffolding exists, unproven/incomplete · CONCEPT = design/enum/fixture only · ABSENT = not in codebase

## Priority tiers
- **T0 — Demo-critical:** required to make the requested insurer demo real and honest.
- **T1 — Month-1 core:** achievable and high-leverage within the window.
- **T2 — Beyond one month:** genuinely larger than a month for the current team; flag for honest sequencing.

---

## A. Core platform (mostly done — protect it)

| Commitment | Status | Work to reconcile | Tier |
|---|---|---|---|
| Human-supervised decision boundary (AI recommends, human decides) | BUILT | None — keep enforced | — |
| Server-side tenant/workspace isolation | BUILT | None | — |
| Audit trail of every consequential action | BUILT | None | — |
| Zero-simulation / honest `Not configured` | BUILT | None | — |
| Multitenant Entra identity | BUILT | None | — |

## B. AI / document intelligence (the value core)

| Commitment | Status | Work to reconcile | Tier |
|---|---|---|---|
| AI extracts/classifies/structures claim documents | PARTIAL (adapters built, never run live) | Provision live Azure Doc Intelligence + OpenAI; prove one real extraction end-to-end | **T0** |
| Prepared claim summary + flagged items to reviewer | PARTIAL | Wire agent-brief + human-review cockpit to real extraction output (Bevaya-style field review w/ source highlighting) | **T0** |
| Grounded policy Q&A (no memory answers) | BUILT (adapter) | Prove against live AI Search + real approved knowledge | T1 |
| Risk signals from non-traditional data (mobile-payment, weather/satellite, field-agent) | CONCEPT | New ingestion + signal surfacing; large | **T2** |

## C. Deployment & infrastructure

| Commitment | Status | Work to reconcile | Tier |
|---|---|---|---|
| Live Azure environment, real providers | ABSENT (IaC only) | Provision dev/staging on real Azure; run startup validator green | **T0** |
| Two-tier deployment (SaaS-hosted + Managed App) | PARTIAL / ABSENT | Stand up SaaS-hosted instance (T0); Managed App packaging (T2) | T0 / **T2** |
| Azure Managed Application packaging established | ABSENT | Build `infra/bicep/marketplace/*`, ARM compile, package | **T2** |
| CI/CD gate enforcement | ABSENT | Add GitHub Actions (build/test/lint/infra) | T1 |

## D. Uganda inclusion & distribution layer (largest gap)

| Commitment | Status | Work to reconcile | Tier |
|---|---|---|---|
| SMS status notifications | **PARTIAL** (`AfricasTalkingSmsProvider` built + unit-tested; live AT credentials + staging prove pending) | Prove live sandbox/prod send; tenant policy wiring beyond platform config flag | **T1** (adapter done) |
| USSD claim initiation | ABSENT | USSD gateway integration + session flow | **T1/T2** |
| WhatsApp document submission | **PARTIAL** (`WhatsAppCloudApi` inbound adapter + webhook + document router; live Meta credentials pending) | Prove live webhook+media download; EF multi-number mapping; outbound WhatsApp | **T1** (adapter done) |
| Photograph capture, 2G/low-bandwidth optimisation | **PARTIAL** (client JPEG downscale + progress + honest offline; 25 MB limits; camera capture on claim review) | Resumable/chunked upload + offline queue deferred — see pending register | **T1** |
| Offline-capable field-agent app + sync | ABSENT | Separate app/PWA + sync engine; large | **T2** |
| Local languages (Luganda, Runyankole, Acholi, Ateso) | ABSENT | i18n framework + translated status/comms templates | T1/T2 |
| Agent-assisted intermediary role & experience | CONCEPT | New role, permissions, simplified agent workspace | T1 |
| Contestability (SMS query + human re-review request) | **PARTIAL** (client-portal create + staff API + Claim Review panel; SMS/USSD entry deferred) | SMS inbound into `ClaimReviewRequest.Create`; optional cross-case inbox | **T1** (case-scoped path done) |
| Mobile-money (MTN/Airtel) premium/payout rails | ABSENT | Payment integration; regulated; large | **T2** |
| SACCO/MFI partner API integration | CONCEPT (generic scaffolding) | Partner onboarding + API contracts | T2 |

## E. Regulatory & governance

| Commitment | Status | Work to reconcile | Tier |
|---|---|---|---|
| Data Protection Act 2019 posture (encryption, minimisation, audit) | PARTIAL | Data-flow register, retention rules, PDPO registration (legal) | T1 (tech) / T2 (legal) |
| IRA engagement / sandbox posture | CONCEPT | External/legal track | T2 |
| Fairness monitoring across demographic segments (bias control) | CONCEPT | Metrics pipeline: approval/settlement rates by segment | T1/T2 |

## F. Traction (not code — real-world)

| Commitment | Status | Reconcile | Tier |
|---|---|---|---|
| Insurer demo requested | REAL (1 company) | Deliver a credible live-ish demo (ties to T0) | **T0** |
| Live pilot / processed claims | CONCEPT | Follows T0 provisioning; then supervised pilot | T2 |

---

## Honest one-month feasibility read

**Achievable in ~1 month (realistic for a small team):** T0 + most of T1 —
live Azure dev/staging, one proven real document-extraction → human-review flow, SMS (Africa's Talking), WhatsApp inbound, agent-assisted intermediary role, contestability flow, low-bandwidth upload, CI/CD, a demo-ready SaaS-hosted instance.

**NOT honestly buildable in one month (T2 — needs more runway or staged delivery):**
offline field-agent app + sync, mobile-money rails, Managed Application packaging + Partner Center, deep local-language coverage, non-traditional risk-signal ingestion, SACCO/MFI production integrations, live insurer pilot with real claims, regulatory certifications.

**Recommendation:** sequence the month around **making the requested insurer demo real and honest** (all T0), because it is the one concrete near-term event and it exercises the whole spine: live Azure → real extraction → agent brief → human review → audit → SMS/WhatsApp touchpoint. Everything T0 is also a permanent capability, not throwaway.

**This is a delivery read, not a push to soften the answers.** The answers stand as the target; this only sequences the build so the highest-value, most-verifiable commitments become true first.
