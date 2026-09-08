# UI migration ledger — design language v2

Tracks screen-by-screen migration of `web/portal` onto the design language v2
foundation (`iip_design_language_v2.md`). One row per screen. A row is marked
**Done** only after it has been opened in a browser, verified against the
27-point checklist in `iip_ui_craft_guardrails.md` §8, and committed.

Resume rule: find the first row not marked Done and migrate that screen next.
Never have two screens in flight.

| # | Tier | Route | Component | Status | Date | Commit |
|---:|---|---|---|---|---|---|
| 1 | 1 | `/app` | `lovable-pages/AIConversations.tsx` | Not started | | |
| 2 | 1 | `/app/work-items` | `lovable-pages/ReviewWorkspace.tsx` (+ UniversalMyWork, AgentWorkItems, Workflows) | Not started | | |
| 3 | 1 | `/app/claims` | `lovable-pages/ClaimsQueue.tsx` | Not started | | |
| 4 | 1 | `/app/claims/[id]` | `lovable-pages/ClaimReview.tsx` | Not started | | |
| 5 | 1 | `/app/claims/[id]/extraction` | `lovable-pages/DocumentExtraction.tsx` | Not started | | |
| 6 | 1 | `/app/documents` | `lovable-pages/Documents.tsx` | Not started | | |
| 7 | 1 | `/app/channels` | `lovable-pages/Channels.tsx` | Not started | | |
| 8 | 2 | `/app/conversations` | `lovable-pages/RecentConversations.tsx` | Not started | | |
| 9 | 2 | `/app/claims/new` | `lovable-pages/FNOL.tsx` | Not started | | |
| 10 | 2 | `/app/customers` | `lovable-pages/Customers.tsx` | Not started | | |
| 11 | 2 | `/app/customers/[id]` | `lovable-pages/CustomerDetail.tsx` | Not started | | |
| 12 | 2 | `/app/communications` | `lovable-pages/Communications.tsx` | Not started | | |
| 13 | 2 | `/app/knowledge` | `lovable-pages/Knowledge.tsx` | Not started | | |
| 14 | 2 | `/app/search` | `lovable-pages/GlobalSearch.tsx` | Not started | | |
| 15 | 3 | `/app/underwriting` | `lovable-pages/Underwriting.tsx` | Not started | | |
| 16 | 3 | `/app/policy` | `lovable-pages/PolicyIntelligence.tsx` | Not started | | |
| 17 | 3 | `/app/fraud` | `lovable-pages/FraudIntelligence.tsx` | Not started | | |
| 18 | 3 | `/app/acquisition` | `lovable-pages/CustomerAcquisition.tsx` | Not started | | |
| 19 | 4 | `/app/flows` | `lovable-pages/AgentFlows.tsx` | Not started | | |
| 20 | 4 | `/app/flows/[key]` | `lovable-pages/AgentFlowInspector.tsx` | Not started | | |
| 21 | 4 | `/app/flows/new` | `lovable-pages/AgentFlowBuilder.tsx` | Not started | | |
| 22 | 4 | `/app/runs/[id]` | `lovable-pages/AgentRunDebugger.tsx` | Not started | | |
| 23 | 4 | `/app/executive` | `lovable-pages/Executive.tsx` | Not started | | |
| 24 | 4 | `/app/audit` | `lovable-pages/Audit.tsx` | Not started | | |
| 25 | 5 | `/app/settings` | `lovable-pages/Settings.tsx` | Not started | | |
| 26 | 5 | `/app/integrations` | `lovable-pages/Integrations.tsx` | Not started | | |
| 27 | 5 | `/app/admin` | `lovable-pages/Admin.tsx` | Not started | | |
| 28 | 5 | `/app/admin/users` | `lovable-pages/admin/Users.tsx` | Not started | | |
| 29 | 5 | `/app/admin/agents` | `lovable-pages/admin/Agents.tsx` | Not started | | |
| 30 | 5 | `/app/admin/tenant-policy` | `lovable-pages/admin/TenantPolicy.tsx` | Not started | | |
| 31 | 5 | `/app/admin/onboarding` | `lovable-pages/admin/Onboarding.tsx` | Not started | | |
| 32 | 5 | `/app/admin/email` | `lovable-pages/admin/EmailInbox.tsx` | Not started | | |
| 33 | 5 | `/app/admin/whatsapp` | `lovable-pages/admin/WhatsAppSettings.tsx` | Not started | | |
| 34 | 5 | `/app/admin/billing` | `lovable-pages/admin/Billing.tsx` | Not started | | |
| 35 | 5 | `/app/support` | `app/app/support/page.tsx` | Not started | | |
| 36 | 6 | `/access` | `lovable-pages/ClaimAccessRequest.tsx` | Not started | | |
| 37 | 6 | `/access/verify` | `lovable-pages/ClaimAccessVerify.tsx` | Not started | | |
| 38 | 6 | `/access/claim` | `lovable-pages/ClaimAccessStatus.tsx` | Not started | | |
| 39 | 6 | `/access/new-claim` | `lovable-pages/ClaimAccessNewClaim.tsx` | Not started | | |

## Foundation slice

Committed `026e576` on `harry/ui-design-language-foundation` — tokens
(`app/globals.css`), shell (`components/iip/AppShell.tsx`, `Sidebar.tsx`),
primitives (`components/ui/`). Screens 1–39 build on this; do not modify the
shell or primitives from a screen-migration run — stop and report instead.

## Run log

- 2026-09-08: ledger created; foundation-slice commit `026e576`.
