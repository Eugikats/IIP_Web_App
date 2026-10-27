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

## Piece status

| # | Piece | State |
|---|---|---|
| 1 | Per-workspace brand and scenario settings | done (backend, API, tests; screen with piece 2) |
| 2 | Demo scenarios and the switch | done |
| 3 | Template catalogue | next |
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

- Piece 2: Domain.Tests 731 passed (9 new DemoScenarioTests), Api.Tests 1575 passed, tsc clean, lint warnings only (pre-existing img warnings). Live check against the offline API on :5167: scenario list and activate work; the bank organisation holds 39 prospects and the default tenant 0; second activate adds nothing. Playwright workspace spec demonstrations.spec.ts passes at 1280 and 768 (3 tests each); screenshots reviewed (Demonstrations page, Nile Life retention with 3, 7, 14, 21, 30 day policies). Not run at 390 px. Known gap: the sidebar workspace chip still shows the workspace id ("main"), not the organisation name.
- Piece 1: dotnet build clean; full Api.Tests 1575 passed (incl. 12 new in WorkspaceBrandTests: workspace isolation, concurrent brands, line focus); Domain.Tests 722 passed. Migration AddWorkspaceRecords generated, not applied to any database.

## Deploy checklist (for when the owner says deploy)

- Build API and portal from this branch, run `db-migrate`, check `/health/ready`, confirm the deploy guard passes (no demo banner).
- Owner actions: submit templates to Meta, Microsoft and Google app registrations, counsel review of the security pack.

## Questions for the owner (asked at the very end)

- None yet.
