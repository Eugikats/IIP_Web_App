# The organisation platform: workspaces, automations, the Intelligence Inbox and intents

> Status: Active
> Owner: Platform Engineering
> Last reviewed: 2026-11-20
> Purpose: the owning document for the organisation layer that sits above the Insurance Intelligence workspace. It states what the layer is, how it was adapted from the CIP implementation, the lifecycle an automation must pass, the routes, permissions, configuration and data, and what is deliberately not built.

Decision record: [`ADR-2026-11-20-organisation-platform-layer`](../06-decisions/ADR-2026-11-20-organisation-platform-layer.md). Shared pattern: `docs/Otic_Universal_Intelligence_Architecture_markdown/Otic_Universal_Intelligence_Architecture.md`.

## 1. What it is

Otic's platforms share one shape: a shell, department **Workspaces**, an **Automations** module with a governed lifecycle, an **Intelligence Inbox** that automations write to, **Intents** that say what the organisation is trying to achieve, and **Reports** and **Conversations** lists. CIP implemented it first; IIP carries the same objects, routes and state machine so the two platforms behave alike.

The six objects:

| Object | Meaning in IIP |
|---|---|
| Intent | An outcome the organisation wants (for example "claims settled within the service deadline"), with a health of `on_track`, `at_risk` or `off_track` derived from open signals |
| Signal | A fact an automation found in IIP data (a claim past its deadline, a policy about to expire) |
| Insight | A card on the organisation overview summarising the inbox and intents |
| Automation | A governed, scheduled watcher that reads IIP data and raises signals |
| Notification | The count and cards the bell and overview show |
| Outcome | The recorded result of a run, kept as an automation run with its findings |

The existing Insurance Intelligence workspace (`/app/workspace`, claims, acquisition, the Command Center) is unchanged. It appears in the organisation layer as the one live primary workspace, created on first use from the `insurance_intelligence` template.

## 2. Naming: tenant workspace and organisation workspace

IIP already has a tenant `WorkspaceId` on every row (`PersistedScopedJson.WorkspaceId`). The department workspaces of this layer are a different thing, so the code calls them **`OrgWorkspace`** and the column that holds their id on automation rows is `OrgWorkspaceId`. The UI says "workspace" because that is what people see; the API path is `/api/v1/org-platform`.

## 3. The automation lifecycle

```text
Draft -> Tested -> AwaitingApproval -> Approved -> Active <-> Paused
                                                     \-> Failed -> (resume) Active
Any state except Retired -> Retired
```

Rules, enforced in `PlatformAutomationService` and mirrored by the buttons the portal offers:

1. A **test** is a dry run over live IIP data. It shows what the automation would raise and writes nothing to the inbox. It can be repeated and is the only action available on a draft besides edit and retire.
2. **Submit** needs the current version to have been tested. Editing what an automation does bumps its version and returns it to `Draft`, so an approver never approves something that was not tested.
3. **Approve**, **reject**, **activate** and **resume** are approver-only (`ApprovePlatformAutomations`). The server decides, not the button: a request from someone without the permission returns 403.
4. Only an **active** automation can **run**. The scheduler, or Run now, raises signals into the inbox. Runs are recorded with their findings.
5. An automation with no runner (the `custom` template) cannot be submitted until its data and logic are configured.

## 4. Where IIP differs from CIP

- **Approvals stay in the existing human-decision flow.** A pending IIP approval appears in the inbox as `apr-{id}` and links to `/app/work-items`. Acting on it from the inbox returns 409. It is shown only to holders of `ApproveSettlementWorkflowStep`. Nothing in this layer approves a claim, a settlement or a payment.
- **Runners are read-only.** They read cases, policies and approvals through `ICaseRepository`, `IPolicyRepository` and `IApprovalRepository` and only raise inbox items. They never change a claim, send a message or call a provider.
- **Insights are the platform's notification cards only.** IIP has no separate workspace insight feed.
- **Reports are automation runs with findings.**
- **Conversations** come from `ICommandCenterConversationService` and link to `/app/conversations/{id}`.
- **Questions go to the Assistant** at `/app/assistant?q=`, which already reads governed IIP data.
- **Storage** is IIP's JSON-row pattern (`PersistedScopedJson` plus indexed columns), not CIP's entity-per-table EF model. The DTO shapes and routes are the same so the TypeScript types and components carry over.

## 5. Automation templates

Shipped in `PlatformTemplates.cs`. All are insurance operations; none decides anything.

| Key | Raises a signal for |
|---|---|
| `claims_sla` | an open claim past its service deadline, or due within N hours (default 24) |
| `claims_stalled` | an open claim not touched for longer than the target |
| `claims_missing_documents` | a claim marked as missing documents for longer than the target |
| `claims_unassigned` | a claim nobody has been assigned to after the waiting time |
| `claims_high_risk` | an open claim the rule engine flagged high risk, so a person reviews it |
| `policies_expiring` | a policy that expires within the window |
| `approvals_waiting` | an approval waiting for a decision longer than the target |
| `daily_briefing` | one summary signal per day |
| `custom` | nothing yet: it has no runner and cannot be submitted until configured |

Open claims exclude `Settled` and `Closed` claims and claims that require confirmation. A policy runner with no policies reports `needs_data` rather than success. Each run raises at most `maxItemsPerRun` items (default 25). Signals are de-duplicated by key: an open item gains an occurrence count, and a dismissed item is not raised again.

## 6. Routes

### API (`/api/v1/org-platform`, `PlatformEndpoints.cs`)

Every route needs `ViewPlatform` and a resolved organisation scope. With no organisation selected the answer is 403, not a server error. Structure changes and approvals are checked again inside the services.

| Route | Purpose |
|---|---|
| `GET /me` | who the caller is, whether they administer the organisation, whether they can approve |
| `GET /templates` | workspace templates, automation templates and connectors |
| `GET, POST /workspaces`, `GET, PATCH /workspaces/{id}` | list, create from a template, read, update |
| `GET, POST /automations`, `GET, PATCH /automations/{id}` | list, create, read with runs, edit |
| `POST /automations/{id}/{action}` | `test`, `run`, `submit`, `approve`, `reject`, `activate`, `pause`, `resume`, `retire` |
| `GET /inbox`, `GET /inbox/counts`, `POST /inbox/{id}/{action}` | list, counts, `acknowledge`, `resolve`, `dismiss`, `reopen` |
| `GET, POST /intents`, `PATCH /intents/{id}`, `POST /intents/{id}/{action}` | intents and their health |
| `GET /notifications`, `GET /insights`, `GET /reports`, `GET /conversations` | overview data |
| `PUT /settings/access` | workspace visibility: `open` or `restricted` (organisation administrators) |

Errors are `ProblemDetails`; the portal shows the `detail` sentence.

### Portal (`web/portal/app/app/platform/`)

| Path | Screen |
|---|---|
| `/app/platform` | Organisation overview: greeting, a question box that opens the Assistant, intelligence cards, intents strip |
| `/app/platform/automations` | Automations list with the Intelligence Inbox and Intents tabs |
| `/app/platform/automations/new`, `/{id}` | Template wizard and the automation detail with its lifecycle controls and run history |
| `/app/platform/workspaces`, `/new`, `/{id}` | Workspace directory, wizard and detail |
| `/app/platform/reports`, `/conversations`, `/settings` | Reports, Conversations, workspace access |

The sidebar has two sets, as in CIP:

- **Workspace set** (every page outside `/app/platform`): a short primary group (Workspace, Claims, Acquisition, Automations), a **More tools** button that opens a grouped list (Work queues, Customers, Policy and risk, Knowledge and learning, Insight and governance, Organisation), then the Manage group and Recent chats. The More tools list includes Organisation overview, All workspaces, Reports and Conversations.
- **Organisation set** (every page under `/app/platform`): Command Center, Workspaces, Automations, Reports, Conversations and Settings, with **Back to workspace** at the top. The workspace chip reads Organisation.

The earlier Automation page (`/app/flows`) is no longer in the sidebar; the route still opens by URL. Specs: `web/portal/tests/workspace/sidebar.spec.ts`. Screens: `docs/screenshots/organisation-platform-2026-11-20/`.

## 7. Permissions

| Permission | Who | Allows |
|---|---|---|
| `ViewPlatform` | ITAdmin, ClaimsOfficer, ClaimsSupervisor, CustomerService, Compliance (Admin has every permission) | read every platform screen |
| `ManagePlatform` | ITAdmin | create and edit workspaces and automations, change workspace access |
| `ApprovePlatformAutomations` | ClaimsSupervisor | approve, reject, activate and resume an automation |

The portal copy of this map is `web/portal/lib/roles.ts`; `pnpm check:action-permission-parity` fails if it drifts from `RolePermissionMap.cs`. The portal's `ROUTE_ACCESS` gates the `/app/platform` routes with `ViewPlatform`.

## 8. Scheduler and configuration

`PlatformAutomationWorker` is a hosted service. Each tick it lists the tenant workspaces, sets a tenant context with the system user `system:platform-scheduler`, and runs the automations that are due.

| Key | Default | Meaning |
|---|---|---|
| `Platform:AutomationsEnabled` | `true` | set `false` to stop scheduled runs (Run now still works) |
| `Platform:AutomationPollSeconds` | `120` | tick interval, clamped to 30 to 900 |

Schedules are **UTC**: hourly, daily at the set time, weekly on Monday. The portal says so next to the schedule.

## 9. Data

Migration `20261120100025_AddOrganisationPlatform` is additive. It creates `OrgWorkspaces`, `PlatformAutomations`, `PlatformAutomationRuns`, `PlatformInboxItems`, `PlatformIntents` and `PlatformSettings`, each a `PersistedScopedJson` row with the indexed columns its queries need. A filtered unique index (`UX_OrgWorkspaces_Primary`, `[IsPrimary] = 1`) allows one primary workspace per scope. Because the in-memory provider has no such index, `EfOrgWorkspaceStore.TryCreatePrimaryAsync` also serialises the check and insert in-process; a page opens with several parallel requests that each make sure the primary workspace exists.

Audit events written: `platform.workspace.created` and `.updated`, `platform.automation.created`, `.updated` and one per lifecycle action, `platform.inbox.{action}`, `platform.intent.{action}`, `platform.access`. The record carries the entity id and the status before and after, never claim content.

## 10. Verification

- `tests/InsuranceIntelligence.Api.Tests/OrganisationPlatformTests.cs`: services over EF InMemory with fake repositories and role-based permissions (lifecycle, approver gate, de-duplication, runners, intent health, schedules).
- `tests/InsuranceIntelligence.Api.Tests/OrganisationPlatformEndpointTests.cs`: the HTTP surface (403 with no organisation, 403 without the permission, one primary workspace under parallel first requests, lifecycle refusal).
- `web/portal/tests/workspace/platform-automations.spec.ts`: template to run to inbox in a browser, on desktop and tablet widths, against the offline API.
- `web/portal/lib/platform/format.test.ts`: display helpers.

## 11. Not built

Shadow mode, a decision journal and a spec compiler (the architecture document names them; CIP does not build them either). A Command Center `platform_overview` tool. A top-bar Intelligence Bell separate from the existing notification bell. Local-time schedules. Real-service proof of the scheduler across instances (it has run only against the offline API).
