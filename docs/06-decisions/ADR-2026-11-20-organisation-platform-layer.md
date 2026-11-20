# ADR: an organisation platform layer above the Insurance Intelligence workspace

> Status: Accepted
> Date: 2026-11-20
> Owner: Platform Engineering
> Owning document: [`iip_organisation_platform.md`](../02-development-engineering/iip_organisation_platform.md)

## Context

Otic set one direction for all its platforms: a shared shell, department workspaces, an Automations module with a governed lifecycle, the Intelligence Inbox and Intents, Reports and Conversations, and a Command Center that matches across products. CIP implemented it first. IIP had a separate Automation page (`/app/flows`) that did not follow the pattern, and no organisation layer.

## Decision

1. Port CIP's design, not a redesign: the same six objects, routes, DTO shapes and state machine (Draft, Tested, AwaitingApproval, Approved, Active, Paused, Failed, Retired), so the two platforms behave alike and the CIP front end carries over.
2. Adapt the storage to IIP: ports in Domain, services in Application, EF JSON-row stores in Infrastructure, minimal-API routes under `/api/v1/org-platform`. Department workspaces are `OrgWorkspace`, distinct from the tenant workspace id on every row.
3. Keep human authority where it is. An automation can only read IIP data and raise inbox items. Approving an automation is a separate permission held by Claims Supervisors. IIP approvals of claims and settlements stay in the existing flow; the inbox links to them and refuses to decide them (409).
4. Replace the sidebar entry for the old Automation page with Automations, and add Organisation, All workspaces, Reports and Conversations under More. The old route is not deleted in this change.
5. Add three permissions (`ViewPlatform`, `ManagePlatform`, `ApprovePlatformAutomations`) and one additive migration.
6. Restyle every copied screen to `DESIGN.md` (borders, one accent, neutral pill plus dot plus word, no tinted surfaces), rather than keeping CIP's appearance.

## Consequences

- IIP gains a scheduler (`PlatformAutomationWorker`). It is on by default and can be switched off with `Platform:AutomationsEnabled=false`. It has run only against the offline API.
- Schedules are UTC.
- The old `/app/flows` page and its code remain until a separate cleanup removes them.
- Shadow mode, a decision journal and a spec compiler are not built, matching CIP.
- Mirroring CIP means CIP changes must be ported deliberately; there is no shared package yet.

## Alternatives considered

Rewriting the module from the architecture document alone (rejected: CIP already proved the behaviour, and a second implementation would drift). Extending the old `/app/flows` page (rejected: it does not model approval, testing or an inbox).
