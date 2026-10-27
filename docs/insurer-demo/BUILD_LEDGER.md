# Insurer demo build ledger

Updated at the end of every iteration. Newest facts win. Keep it short.

Worktree: `Projects/iip_saas_insurer_demo`, branch `build/insurer-demo-2026-10-27`, from dff5ab3. Local only, no deploys.

## Decisions made (owner away, recommended choices)

- One generic per-workspace store, table `WorkspaceRecords` (tenant, workspace, kind, key, JSON), behind `IWorkspaceRecordStore`. Brand, templates, rules, events, decision log and outcomes use it, so each piece needs no migration of its own. It carries a tenant id, so export and purge cover it automatically. Event volume can move to its own table later if a bank sends large files.
- Whose assistant Kato is: `KatoBrand.Current` now reads an ambient per-request brand (AsyncLocal, `KatoBrand.Use`) and falls back to the start-up default. Webhook inbound, outreach drafting, triage and conversation insights resolve the workspace brand at their entry points. The `Assistant:*` settings remain the default for workspaces that set nothing.
- Life versus general is `InsuranceClass` on each product (loan protector, funeral and school fees are life; the rest general) and `LineFocus` on the brand filters products by it.
- Brand API is `GET/PUT /api/v1/workspace/brand`, ConfigureIntegrations permission, audited (`workspace.brand.saved`). Its screen comes with piece 2 (scenario switch) and piece 14 (company profile).
- Nile Life is a separate life company in the Nile group, "Nile Shield Life Assurance", with three life covers (loan protector, family funeral, school fees) built from the partner life covers. Nile General is the existing "Nile Shield Assurance" restricted to general covers. `MicroinsuranceCatalog.Offered` holds every cover that can be sold; `All` is unchanged. Lookups by product line must go through `KatoBrand.Find` because several insurers share a line.
- A demonstration scenario is an organisation of its own (name ends "(demonstration)"), created for the signed-in person who becomes its admin, marked `IsDemonstration` with its `ScenarioId` in the brand record. Switching is the portal choosing which organisation to act in (localStorage override sent as X-Organisation-Id, or X-Tenant-Id in local mode, plus the workspace). Never available under a protected runtime profile; the Admin tile shows only in Demo, LocalTest and Development profiles. Re-running setup is idempotent and refreshes relative dates.
- Mavuno Bank loads the existing 39-person invented bank list (consent recorded). Eugene is added by piece 11 (demo customer form), because his phone must be a real consenting teammate's.

- Templates: stored as `template` records in the workspace store, keyed name:language. A workspace shows the 12 standard drafts (the plan's 11 plus `bank_cover_intro`) until it has its own. Only a person records WhatsApp's decision (submit, approve, reject with reason, pause) until provider sync exists; an owner can import a JSON file of templates that keeps its status. An approved template is never edited in place. The Cloud API provider now sends numbered placeholders in order without a parameter name (named placeholders still carry theirs).
- First-touch sending without a prior inbound message: `TemplateMessageService` sends an approved template to a person who has not written in, when the caller has checked consent and names it. It refuses a draft, a withdrawn consent, a missing value, marketing outside 07:00 to 20:00 EAT, an unconnected number and an unconfigured provider; a retry with the same key never sends twice. The existing free-text outreach flow is unchanged and still needs an open window.

- Events: nine event types from plan 14.3 (code, label, reason, suggested offer, timing). An event stores phone (normalised to 256...), type, when, source and only four safe details (amount, term_months, product_code, branch); a balance or account number column is never read and is named as left out. The id is a hash of type, phone, day and the bank's own reference, so a resent file changes nothing. Phones are masked in the list API. Acquisition has a new "Bank events" tab (record form with "when" choices for rehearsal, CSV upload).
- Connected folders can be marked `purpose: BankEvents` (API field on the folder entry): CSV files are read by `IBankEventFileSink`, state `Imported`, never become knowledge, never retired. Tested with a fake provider only. The folder picker screen has no purpose control yet; add it in piece 15 with the storage step.

## Piece status

| # | Piece | State |
|---|---|---|
| 1 | Per-workspace brand and scenario settings | done (backend, API, tests; screen with piece 2) |
| 2 | Demo scenarios and the switch | done |
| 3 | Template catalogue | done |
| 4 | Events (store, form, file import) | done |
| 5 | Rules engine | next |
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

- Piece 4: Api.Tests 1617 passed (17 BankEventTests, 5 connector event-folder tests), Domain.Tests 731, tsc clean, lint 0 errors. Playwright bank-events.spec.ts 4 tests pass at 1280 and 768; tablet screenshot reviewed. Not run at 390 px.
- Piece 3: Api.Tests 1595 passed (20 new in TemplateCatalogTests), Domain.Tests 731, tsc clean. Playwright templates.spec.ts 4 tests pass at 1280 and 768; screenshot of the draft drawer reviewed. Not run at 390 px. The send path is proven only against a recording fake provider; no real WhatsApp call was made.
- Piece 2: Domain.Tests 731 passed (9 new DemoScenarioTests), Api.Tests 1575 passed, tsc clean, lint warnings only (pre-existing img warnings). Live check against the offline API on :5167: scenario list and activate work; the bank organisation holds 39 prospects and the default tenant 0; second activate adds nothing. Playwright workspace spec demonstrations.spec.ts passes at 1280 and 768 (3 tests each); screenshots reviewed (Demonstrations page, Nile Life retention with 3, 7, 14, 21, 30 day policies). Not run at 390 px. Known gap: the sidebar workspace chip still shows the workspace id ("main"), not the organisation name.
- Piece 1: dotnet build clean; full Api.Tests 1575 passed (incl. 12 new in WorkspaceBrandTests: workspace isolation, concurrent brands, line focus); Domain.Tests 722 passed. Migration AddWorkspaceRecords generated, not applied to any database.

## Deploy checklist (for when the owner says deploy)

- Build API and portal from this branch, run `db-migrate`, check `/health/ready`, confirm the deploy guard passes (no demo banner).
- Owner actions: submit templates to Meta, Microsoft and Google app registrations, counsel review of the security pack.

## Questions for the owner (asked at the very end)

- None yet.
