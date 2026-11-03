# IIP — Start-Here Context Index

Domain vocabulary is canonical in [`../CONTEXT.md`](../CONTEXT.md). This file is only the repository navigation index.

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

## Product and experience structure
- `docs/03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md` — canonical staff object/findability model, sitemap, labels, permission-derived navigation, route ownership, screen/state inventory, and practitioner-validation gate.
- `docs/03-design-architecture/iip_platform_ux_behavior_specification.md` — platform interaction behavior and operating model.
- `docs/03-design-architecture/iip_end_to_end_user_flow_architecture.md` — workflow behavior, decisions, exceptions, and recovery.

## Reference research (read when relevant)
- `docs/research/bevaya-comparative-analysis.md` — competitor benchmark (adopt UX/workflows; reject its visual design).
- `docs/research/sms-auto-send-policy.md` — Uganda DPPA/UCC-grounded SMS auto-send policy behind `OutboundSmsGuard`.

## Assets
- `prototypes/iip-undp-demo.PROTOTYPE.html` — clickable UNDP demo (serves the requested insurer demo).

## Readiness status
Do not maintain a readiness snapshot in this index. Use `docs/08-program-plan/IIP-CONSOLIDATED-ROADMAP.md`,
`docs/07-qa/rf10-release-foundation-go-no-go-evidence.md`,
`docs/07-qa/release-1-candidate-readiness-record.md` (candidate commit, known-gaps registry, motor scope),
and current deployment/test evidence. The current RF-10 production-candidate verdict is **NO**; revalidate before making any newer claim.