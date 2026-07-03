# Workstream 2 — Command Center Frontend Vertical Slice

**Deliverable type:** Next.js/React/TypeScript source overlay  
**Repository target requested:** `web/portal/`  
**Status:** Source produced; real in-place implementation blocked because no existing `web/portal` project is mounted in the sandbox

## Inspection result

I searched the sandbox for an existing portal project:

```text
find /mnt/data -maxdepth 5 -name package.json -o -name pnpm-lock.yaml
```

No existing `web/portal/package.json`, `pnpm-lock.yaml`, or app source tree was present. Therefore I did not modify a real frontend repository in place. I produced a source overlay under `web/portal/` that should be reconciled into the real repository using its actual conventions.

## Produced files

- `web/portal/src/app/command-center/page.tsx`
- `web/portal/src/app/dashboard/page.tsx`
- `web/portal/src/components/AppShell.tsx`
- `web/portal/src/components/command-center/CommandChatWorkspace.tsx`
- `web/portal/src/components/command-center/CommandChatThread.tsx`
- `web/portal/src/components/command-center/CommandMessage.tsx`
- `web/portal/src/components/command-center/CommandComposer.tsx`
- `web/portal/src/components/command-center/CommandBlocks.tsx`
- `web/portal/src/components/command-center/EvidenceRail.tsx`
- `web/portal/src/lib/api/commandCenterClient.ts`
- `web/portal/src/lib/auth/msalTokenProvider.ts`
- `web/portal/src/types/commandCenter.ts`
- Component tests for block rendering and composer-to-thread render flow

## Real wiring implemented in the overlay

- `POST /api/command-center/query` is used directly.
- No mock response is used in runtime code.
- Bearer token acquisition uses MSAL; no hardcoded fake token is present.
- `/command-center` is the landing implementation route.
- `/dashboard` redirects to `/command-center` as a legacy alias.
- Rendered backend block types: `text`, `metric_pills`, `sla_summary`, `not_configured`, `error`.
- `EvidenceRail` renders `EvidenceSourceDto[]`.

## Validation status

`pnpm` is not installed in this sandbox and no real `web/portal` project was mounted, so I could not run:

```text
pnpm build
pnpm lint
pnpm test
```

Actual environment check:

```text
node --version -> v22.16.0
pnpm --version -> bash: line 1: pnpm: command not found
```

## Auth status

Runtime code uses MSAL and requires these environment values:

```text
NEXT_PUBLIC_ENTRA_CLIENT_ID
NEXT_PUBLIC_ENTRA_TENANT_ID or organizations
NEXT_PUBLIC_ENTRA_REDIRECT_URI
NEXT_PUBLIC_IIP_API_SCOPE
NEXT_PUBLIC_IIP_API_BASE_URL
```

Full token-refresh and account-selection UX remains a blocker for the next frontend pass. The overlay fails closed if auth is not configured; it does not use a fake token.

## Completion report

No fake dashboard stats, hardcoded insurer data, or mock role switchers were introduced. The only mocked behavior is inside unit tests. Out of scope block types and other module screens were not implemented.

**Next frontend step:** apply this overlay into the real `web/portal` project, map import aliases to the existing conventions, add/confirm MSAL package dependencies, then run `pnpm build`, `pnpm lint`, and `pnpm test` in the real repo.
