# IIP — Start-Here Context Index

A **map, not a mirror.** Read this first to orient cheaply, then open only the deep artifact you need.
Keep it thin: point to sources, don't restate them. **Freshness rule:** every shipped slice must update
the capability map and the pending-work register — treat a PARTIAL adapter as PARTIAL until its pending
row is CLOSED with evidence.

## Authoritative contracts (always read before changing behavior)
- `AGENTS.md` — product/architecture/security/UI/deployment/release contract (highest authority).
- `IIP_IMPLEMENTATION_GUARDRAILS.md` — shared implementation guardrails for all agents.
- `CLAUDE.md` — agent execution rules + UI authority doc list.

## Current state & backlog (read when planning work)
- `docs/research/iip-current-state-capability-map.md` — what's BUILT / PARTIAL / CONCEPT / ABSENT + founder-confirmed ground truth.
- `docs/research/iip-commitment-reconciliation-backlog.md` — UNDP answers as binding spec; T0/T1/T2 build backlog.
- `docs/research/iip-pending-work-register.md` — open deferred rows (PEND-*); close or re-defer when touching related code.
- `docs/research/iip-session-handoff-2026-08-03.md` — latest session handoff (branch tips, next slices, validation run).

## Reference research (read when relevant)
- `docs/research/bevaya-comparative-analysis.md` — competitor benchmark (adopt UX/workflows; reject its visual design).
- `docs/research/sms-auto-send-policy.md` — Uganda DPPA/UCC-grounded SMS auto-send policy behind `OutboundSmsGuard`.

## Assets
- `prototypes/iip-undp-demo.PROTOTYPE.html` — clickable UNDP demo (serves the requested insurer demo).

## One-line status
Real, tested .NET 8 + Next.js platform at ~Production Release Gate 1–2. Core (tenant isolation, audit,
human-review ceiling) BUILT. Uganda channel adapters (SMS/WhatsApp/upload) PARTIAL — config-gated, unit-tested,
**not live-proven** (blocked on credentials + live Azure staging). No live deployment yet. Not production-ready.
