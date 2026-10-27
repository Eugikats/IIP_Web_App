# Insurer demo build ledger

Updated at the end of every iteration. Newest facts win. Keep it short.

Worktree: `Projects/iip_saas_insurer_demo`, branch `build/insurer-demo-2026-10-27`, from dff5ab3. Local only, no deploys.

## Decisions made (owner away, recommended choices)

(none yet)

## Piece status

| # | Piece | State |
|---|---|---|
| 1 | Per-workspace brand and scenario settings | next |
| 2 | Demo scenarios and the switch | |
| 3 | Template catalogue | |
| 4 | Events (store, form, file import) | |
| 5 | Rules engine | |
| 6 | Reason carried into the conversation | |
| 7 | Outcomes and hand-off, STOP | |
| 8 | Bank first-touch flow | |
| 9 | Renewal and premium flows | |
| 10 | Document chasing and claim updates | |
| 11 | Demo customer and timeline screens | |
| 12 | Conversion view for bank staff | |
| 13 | Campaign reporting | |
| 14 | Onboarding: company profile, terms, licence | |
| 15 | Onboarding: security screen, storage step | |
| 16 | Extraction agent for wordings | |
| 17 | "Here is what I found" confirm screen | |
| 18 | Managed WhatsApp number flow | |
| 19 | Claim by WhatsApp polish and identity check | |
| 20 | Security pack | |
| 21 | Rehearsal | |

## Verified so far

- Nothing yet.

## Deploy checklist (for when the owner says deploy)

- Build API and portal from this branch, run `db-migrate`, check `/health/ready`, confirm the deploy guard passes (no demo banner).
- Owner actions: submit templates to Meta, Microsoft and Google app registrations, counsel review of the security pack.

## Questions for the owner (asked at the very end)

- None yet.
