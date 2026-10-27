# Insurer demo build loop: instructions for every iteration

You are building the insurer and bank demo layer in the git worktree `Projects/iip_saas_insurer_demo` (branch
`build/insurer-demo-2026-10-27`, taken from `saas/merge-v2-2026-10-08` at dff5ab3). Do not edit `Projects/iip_saas` (another
agent has uncommitted work there) or `Projects/iip_saas_release`. The owner is away and cannot answer questions. Do not
stop to ask. Make the recommended choice, record it in the ledger, and carry on. Put questions for the owner in the
ledger's "Questions for the owner" list; they are asked at the very end. Stop only when all 21 pieces are done and
verified locally, or when a real blocker needs a person.

This is one continuous pass, not phases. Work the pieces in order, one after another, until all 21 are done.

## Read first, every iteration

1. `docs/insurer-demo/BUILD_LEDGER.md`: what is done, what is next, decisions, questions. It is the memory.
2. `docs/insurer-demo/IIP_INSURER_MEETING_READINESS_PLAN.md`: the plan. Section 15 is the ordered build (21 pieces),
   sections 4, 10, 11 and 14 hold the templates, scenarios, bank first-touch flow, event triggers and rule anatomy.
3. `docs/insurer-demo/IIP_BANK_QUESTIONS_ANSWERED_FROM_CIP.md` and `IIP_BANK_TECHNICAL_QUESTIONS.md` (piece 20 uses them).
4. Repo rules: `CLAUDE.md`, `AGENTS.md`, `DESIGN.md` for anything a user can see, and memory: no em dashes anywhere, no
   emoji, no fabricated data, six UI states (loading, empty, empty after filter, error, not configured, no permission),
   honest "Not configured". Commit footer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Decisions already made by the owner

- Three demo workspaces: **Mavuno Bank** (bancassurance, sells life and general from partners), **Nile Life**, **Nile General**.
- Eugene (demo customer): Mavuno Bank, Ntinda branch; salary account, savings account, salary loan UGX 8m over 36 months
  disbursed 2 days ago; salary about UGX 2.4m a month; 2 children; consented to bank insurance marketing; no cover held.
- Outreach runs on a rule approved once (rule and wording, not each message). Which roles may approve a rule is set by the
  bank admin, per organisation.
- IIP does not record commission rates for now. The bank view shows conversions only.
- Bank data arrives as batch events or files from a data lake that IIP reads. Start with a person recording an event and a
  file import; a live feed is later.
- Templates: the owner will upload approved templates later. Build the catalogue with draft templates (status Draft or
  Awaiting approval), a local stand-in provider for tests, and never assume Meta approved anything.
- Managed WhatsApp number flow is ported from CIP (`CIP_Web_App`: MetaPhoneNumberClient, WhatsAppOnboardingService).
- Local only. No deploys, no cloud changes, no push, no merge to main. Leave a deploy checklist in the ledger at the end.

## Method each iteration

1. Read the ledger. Take the first unfinished piece.
2. Inspect the existing code first (search before writing). Prefer extending existing services, endpoints and
   components over new architecture. Domain rules stay in Domain/Application, adapters in Infrastructure.
3. Build it complete: backend, persistence (EF migration when a table is added; every new table carries tenant and
   workspace id, or is listed as platform data on purpose), API with server-side authorization and audit, UI with all six
   states, tests.
4. Verify: `dotnet build`, targeted `dotnet test` while iterating and the full suite before closing a group of pieces,
   `pnpm lint`, `tsc --noEmit` in `web/portal`, and for UI open the screen in a real browser at 1280, 768 and 390 px
   (Playwright) and look at it. Fix what looks wrong. Do not claim something passed that did not run.
5. Commit with an honest message (one commit per piece or sub-slice). Update the ledger: done, next, decisions, what was
   verified and what was not.
6. Continue straight to the next piece. Do not end a turn on a status message while work remains.

## Hard rules

- Tenant and workspace isolation everywhere; a test for every new store. Server-side authorization on every endpoint.
- AI prepares, humans decide. Kato never approves, rejects, binds, or sets a premium. Commission never affects ranking,
  and Kato tells a customer who asks that the bank earns a commission from the insurer.
- Outreach gates stay: consent, not already covered, not contacted recently, sending hours, no open complaint, 24-hour
  window or approved template, STOP honoured. A decision is logged whether sent or skipped, with the reason.
- Demo data is labelled as demonstration, lives only in demo workspaces, uses dates relative to today, and the scenario
  switch is never shown in a workspace with real customers. No yellow "Synthetic demo data" banner and no bottom status bar
  anywhere (the owner has removed them repeatedly; do not reintroduce them).
- Secrets never in the repo or chat. Do not weaken security, tenant isolation or two-factor to make a check pass.
- No em dashes or emoji in code, comments, UI, docs or commits.
- Do not delete or overwrite the owner's files. Do not push or merge to main.

## The 21 pieces (the ledger tracks them)

1 per-workspace brand and scenario settings. 2 demo scenarios and the switch. 3 template catalogue. 4 events (store, form,
file import). 5 rules engine. 6 reason carried into the conversation. 7 outcomes and hand-off, STOP. 8 bank first-touch
flow. 9 renewal and premium flows. 10 document chasing and claim updates. 11 demo customer and timeline screens. 12
conversion view for bank staff. 13 campaign reporting. 14 onboarding company profile, terms, licence. 15 onboarding
security screen and storage step. 16 extraction agent for wordings. 17 "here is what I found" confirm screen. 18 managed
WhatsApp number flow. 19 claim by WhatsApp polish and identity check. 20 security pack. 21 rehearsal.
