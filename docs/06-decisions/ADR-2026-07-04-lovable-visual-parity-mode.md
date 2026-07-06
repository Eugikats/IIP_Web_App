# ADR 2026-07-04: LovableVisualParity development mode

## Status

Accepted for local development migration only.

## Context

The Lovable/Vite export at `C:\Users\Nic Kings\AppData\Local\Temp\78bfc38c-38a5-4e2f-b414-863e3fd0a9bb_Intelligent Operations Hub.zip.9bb` is the temporary visual and workflow source of truth for the IIP Operations portal migration. Several existing portal routes already have backend-backed layouts, but the current migration goal is visual parity with the Lovable export before the backend contract reconciliation pass.

The repository production contract still prohibits mock, simulated, fallback, or fabricated operational behavior in `Staging`, `Production`, `ManagedApplicationReviewer`, and customer-facing deployments.

## Decision

Introduce `LovableVisualParity` as a development-only frontend migration mode, enabled by:

```text
NEXT_PUBLIC_VISUAL_PARITY_MODE=true
```

When this mode is enabled, the Next.js App Router portal may render Lovable-equivalent screens using typed local fixtures and prototype UI actions from `web/portal/lib/lovable-parity/`.

This mode may replace the current visual layout of API-backed frontend screens during migration. It does not change backend authorization, tenant resolution, provider validation, or production runtime rules.

## Constraints

- `LovableVisualParity` is not a deployable production behavior.
- Static fixtures and prototype actions must remain under `web/portal/lib/lovable-parity/`.
- Supabase export files are not imported.
- Future backend contracts remain under `/api/v1/...`.
- Staging, Production, ManagedApplicationReviewer, and customer deployments must not silently use parity fixtures.

## Follow-up backend contract targets

- claims queue
- claim detail and review
- document extraction
- communications
- channels
- customers and customer detail
- acquisition leads
- policy intelligence
- fraud and risk advisory
- underwriting
- workflows and approvals
- knowledge
- executive reporting
- integrations and provider health
- audit
- admin agents, billing, and tenant policy
