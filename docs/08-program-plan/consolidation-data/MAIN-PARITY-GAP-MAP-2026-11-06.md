# Main parity gap map: what is left so `main` has every API connected and every page working

> Status: Evidence
> Date: 2026-11-06
> Compares: `main` at `495db1ad` against the live lineage `live/insurer-demo-2026-10-27-4818bb1`
> Written for: Codex and the owner, to finish the consolidation before `main` is deployed again.

## 1. What happened

`main` was deployed to the dev environment on 2026-11-06 and rolled back the same morning. Probing the running API showed that routes the live build serves return 404 on `main`: demo scenarios, workspace brand, bank events, rules, templates, WhatsApp receipts, organisations, billing credits and the whole Academy API. The Academy page showed "The courses could not be loaded", and the Bank events, Rules and WhatsApp delivery screens were off.

The cause is not missing business code. Every Application, Domain and Infrastructure folder in the live build exists in `main`, and the endpoint classes exist. What was lost is the **wiring in `Program.cs`** (route maps, service registrations, and the authentication and middleware setup), most likely in a conflict resolution during phase B or C. The test suite did not notice because no test calls these routes over HTTP: the live build has no HTTP-level tests for demo, rules, events, templates or workspace brand, and one file each for Academy, organisations and security.

## 2. The gap, by layer

| Layer | Live | Main | Gap | Evidence file |
|---|---|---|---|---|
| API routes registered in `Program.cs` | 429 | 362 | **172** live routes missing; **152** distinct paths appear nowhere in `main` source | `main-missing-routes-2026-11-06.txt` |
| Service registrations in `Program.cs` | | | **56** statements only in live | `main-missing-di-registrations-2026-11-06.txt` |
| Other `Program.cs` statements (authentication schemes, middleware, options, CORS, data protection) | | | **102** statements only in live | `main-missing-program-other-2026-11-06.txt` |
| `InfrastructureRegistration.cs` | | | 75 statements only in live (37 only in main) | compare the two files |
| `RuntimeHeaderGuard.cs` | | | 9 statements only in live (14 only in main) | compare the two files |
| Portal pages | 85 | 100 | **none missing**; main adds 15 | none needed |
| Portal calls to routes the same build does not serve | 18 (matcher noise) | 77 | **59** calls that are dead only in `main` | `main-dead-portal-calls-2026-11-06.txt` |
| Tests | 214 .cs files | 298 | none lost except two files superseded by newer ones | none needed |

The 18 baseline "dead calls" present in both builds are limits of the matching script (template strings and route groups), not defects.

### 2.1 Missing routes by area (152 distinct paths)

| Area | Paths | What stops working in the product |
|---|---|---|
| `/api/v1/academy` | 50 | The whole Academy: courses, enrolment, assessments, certificates, payments, Zenith. This is the "courses could not be loaded" error |
| `/api/v1/organisation(s)` | 22 | Organisation settings, members, invitations, broker links |
| `/api/v1/security` | 18 | Platform MFA and passkeys |
| `/api/v1/me` | 8 | My organisations (the world and organisation switch), connected file accounts |
| `/api/v1/integrations` | 7 | WhatsApp receipts, Meta delivery check, managed onboarding, the integration catalogue, test send |
| `/api/v1/connectors` | 7 | File and SharePoint connectors |
| `/api/v1/rules` and `/api/v1/rule-*` | 14 | Bancassurance rules, approval, run, outcomes, decisions, conversions, campaigns, timeline |
| `/api/v1/templates` | 5 | WhatsApp template catalogue and approval record |
| `/api/v1/events` | 4 | Bank events and their import |
| `/api/v1/demo` | 4 | Demonstration worlds (Mavuno Bank, Nile Life, Nile General) and the switch between them |
| `/api/v1/marketplace` | 3 | Marketplace activation and webhook |
| `/api/v1/admin` | 3 | Organisation adoption and managed WhatsApp onboarding queue |
| `/api/v1/products`, `/exports`, `/compliance`, `/billing`, `/workspace` | 8 | Policy wording extractions, exports, compliance pieces, billing credits, workspace brand |

### 2.2 Dead portal calls in `main` by area (59)

integrations 10, admin 6, rules 5, organisation 5, demo 4, templates 3, events 3, channels 3, rule-outcomes 2, products 2, customer-follow-ups 2, compliance 2, cases 2, and one each for workspace, rules-run, rules-approval, rule-timeline, rule-conversions, rule-campaigns, me, customers, channel-identities and billing.

Not every one is a lost route: `channels`, `customer-follow-ups`, `cases`, `customers` and `channel-identities` calls may be routes that were renamed when the v2 social-channel work and claims-core merged. Each needs a decision: restore the route, or update the portal call.

## 3. What this means for the three things the owner presents

| Need | Blocked on `main` by |
|---|---|
| Switch to Mavuno Bank and see its data | `/api/v1/demo/*`, `/api/v1/me/organisations`, `/api/v1/workspace/brand` |
| Bank events, Rules, Conversions, Outreach tabs | `/api/v1/events*`, `/api/v1/rules*`, `/api/v1/rule-*`, `/api/v1/templates*` |
| WhatsApp: delivery panel, receipts, number onboarding | `/api/v1/integrations/whatsapp/receipts`, `meta-check`, `onboarding/*` |
| Academy | the whole `/api/v1/academy` area (50 paths) |

## 4. What is left to do, in order

1. **Rebuild `Program.cs` from the live file as the base, then re-apply what `main` added.** The live `Program.cs` is the winner under plan section 4 (the live deployment wins unless the other side is provably a superset in tested behaviour). Start from the live statements, then add back every statement that exists only in `main` (route maps and registrations for claims-core, the Kato workspace, list import and v2 social channels). Resolve each statement that exists in both, in the order of the two evidence files. Do not hand-pick routes from the list: take the whole block per area, so a route and its service registration move together.
2. **Authentication and middleware first, because routes depend on them.** The 102 "other" statements include the `IipAuth` policy scheme (bearer token, API key, external identity), the `MultiTenant` and external-identity options, the WebAuthn origins, data protection to blob storage with Key Vault keys, the organisation header handling, and the exclusion of `/api/v1/me/organisations` from membership resolution. The organisation header is how the portal acts inside a demonstration world, so losing it breaks the world switch even if the routes come back. Reconcile with the claims-core sign-in (local credentials and password sign-in) by a written capability matrix, not by deleting either.
3. **Service registrations.** Restore the 56 registrations and the 75 in `InfrastructureRegistration.cs` that `main` lacks, and check each against a class that exists (every one should).
4. **Routes by area, in this order:** demo and `me` and `workspace` (world switch), events and rules and templates (bank demo), integrations (WhatsApp), organisations, academy, security, marketplace, connectors, billing, products, exports, compliance, admin.
5. **Decide the 59 dead portal calls** one by one: restore, or change the portal call. Record each decision in `DECISIONS.md`.
6. **Add the missing safety net: a route-parity test.** Add an HTTP-level test that builds the application and asserts a required list of method and path pairs (taken from the live build, 429 entries, plus `main`'s additions) is registered, and a second that asserts every `/api/v1/...` path the portal calls is served. This is the test whose absence let the loss through. It must fail on the current `main`.
7. **Probe the running build, not just the code.** After deploying to dev, call the routes in section 3 with a signed-in token and confirm none returns 404, as was done on 2026-11-06 (an unmapped route returns 404; a mapped one returns 200, 400, 401 or 403).

## 5. Gate before `main` is deployed again

- The route-parity tests above pass, and both fail when run against the 2026-11-06 `main`.
- Backend: 0 warnings and 0 errors, all tests pass, no test removed.
- Portal: `pnpm lint`, `pnpm test`, `pnpm build`, and the claims-core and visual suites from plan section 8.
- A rehearsal in dev with the real sign-in: switch to Mavuno Bank, open every Acquisition tab, open Admin, WhatsApp Business, open the Academy, and send one WhatsApp message through to the Inbox.
- The deploy order is API, restart and wait for a build-specific route to answer, apply the migration, then portal, restart, verify. The first API deploy of 2026-11-06 reported success while the old build kept running until a restart, so verification must check a route that exists only in the new build.
- A rollback package of the build being replaced is built and kept before any deploy.

## 6. What is already fine

- All Application, Domain and Infrastructure code from the live build is present in `main`.
- The portal has every live page and 15 more.
- The database migration `Consolidate_ClaimsCore` is additive and applied cleanly on dev; the live database is now at 43 migrations and works with both builds.
- Claims-core, the Kato workspace, list import fixes and the WhatsApp number-ownership fixes are in `main` and tested.
