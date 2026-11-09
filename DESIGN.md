# DESIGN.md — UI and UX authority for IIP

Read this before changing anything a user can see. It is the entry point; the four documents it links are the detail. `AGENTS.md` invariants outrank everything here — a visual rule never justifies weakening security, tenant isolation, human authority, or honest state.

## The four documents

| Document | Owns | Read it when |
|---|---|---|
| [`docs/03-design-architecture/iip_design_language_v2.md`](docs/03-design-architecture/iip_design_language_v2.md) | Tokens, colour, type, spacing, radius, elevation, motion, shell geometry, ~25 component contracts, migration map | Changing how anything looks |
| [`docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md`](docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md) | Navigation model, the workspace landing, chat-to-work handoff, progressive disclosure, toast and interruption policy, forms | Adding a screen, a destination, or any notification |
| [`docs/03-design-architecture/iip_workflow_legibility_contract.md`](docs/03-design-architecture/iip_workflow_legibility_contract.md) | The claim lifecycle as rendered: stage map, completeness display, evidence and confidence, the human-decision boundary, the product-wide status vocabulary | Rendering a claim, work item, document, or agent output |
| [`docs/03-design-architecture/iip_ui_craft_guardrails.md`](docs/03-design-architecture/iip_ui_craft_guardrails.md) | The craft floor: banned patterns, the emoji ban, copy rules, the 27-point pre-merge checklist, automated enforcement | Before opening any UI pull request |

Visual reference for the language: [`docs/03-design-architecture/reference/bevaya-design-language.md`](docs/03-design-architecture/reference/bevaya-design-language.md) and [`docs/03-design-architecture/reference/bevaya-ui-replica.html`](docs/03-design-architecture/reference/bevaya-ui-replica.html). Open the HTML in a browser to see the target. Copy its visual grammar; do not copy its product scope, and do not copy its emoji.

## The direction, in short

A calm, border-led, light-first insurance operations console. One blue accent (`#1769FF`, the same blue as the CIP platform) used only to mark the next action. Inter throughout, with an expressive workspace greeting. Hairline borders instead of shadows. Colour rationed to 6px status dots and single figures. An 80px collapsed icon rail, a contextual panel, and a left-aligned content column. It should read as **a precise instrument, not a product tour.**

## Accent colour decision (2026-10-07)

IIP is CIP's sister product and shares its look, so the accent changed from violet `#5B2AE8` to CIP's blue `#1769FF` across the portal. It is a token change (`--color-primary` and its tints in `app/globals.css`, `--violet` in `app/lovable.css`), so a component that uses the tokens follows it. The acquisition dashboard and the inbox are ported from CIP's own components (`components/cip/`) and use CIP's tinted status pills; every other screen keeps the neutral pill. The longer-term plan to write one shared design language for both products is outcome O of `docs/01-strategy-requirements-roadmap/IIP_BANCASSURANCE_ACQUISITION_BUILD_PROMPT.md`. The older documents linked above still describe violet; where they disagree with this section, this section wins until they are rewritten.

## The rules that are never waived

1. **No emoji anywhere** — UI, code, chat output, commit messages, docs. Guardrails §3.
2. **One primary action per screen region, in the accent blue.** The accent marks what to do next; it never decorates.
3. **Gradients in exactly two places** — the Assistant orb and chart bars. Nowhere else.
4. **Borders, not shadows.** Static cards carry `box-shadow: none`. Shadow is only for genuinely floating surfaces.
5. **Status is a neutral pill plus a 6px coloured dot plus a word.** Never colour alone, never a tinted pill, never coloured pill text.
6. **Every screen answers five questions on sight**: where am I, what is this, what needs me, what next, how do I get back. Navigation model §4.
7. **Six states or it is not finished**: loading, empty, empty-after-filter, error, not configured, no permission.
8. **No fabricated data.** No fake numbers, placeholder names, dead buttons, or "coming soon". Missing capability renders `Not configured`.
9. **AI prepares; humans decide** — and the interface shows the difference. Prepared work sits in the AI narrative card; the decision sits in a separate neutral decision bar with an explicit verb and a consequence preview.
10. **Every actionable AI assertion carries a citation** that opens its source. No citation, no render.
11. **No em dashes anywhere.** UI copy, code, comments, model output, commit messages and docs. A comma, colon, full stop or parentheses reads better and does not mark the text as machine-written. Guardrails section 3.

## Where things live

- Landing screen is the **Workspace** at `/app/workspace`. It greets the signed-in staff member, links only to queues their permissions allow, and retains the Kato acquisition workbook, chart and export workflow for authorized staff. The Brief remains at `/app/brief`; the Assistant is at `/app/assistant` and persisted conversations reopen from Recent chats. Queue counts appear only when supplied by permitted live data.
- The icon rail groups destinations rather than capping them at seven; see navigation model §5.2 (amended 2026-09-11) for why and §6 for the landing decision.
- Toasts are rare and governed: off-screen, this user's work, actionable now, not already shown. Everything else is a badge, an inline message, or the notification bell. Navigation model §8.

## Before you merge

Run the 27-point checklist in [`iip_ui_craft_guardrails.md`](docs/03-design-architecture/iip_ui_craft_guardrails.md) §8. One "no" blocks the merge. Then run `pnpm lint`, `pnpm build`, `pnpm test` from `web/portal` and report the real results.

## Superseded documents

The following remain in the repository for history. Do not build from their visual guidance.

- `docs/03-design-architecture/iip_visual_design_system_and_tokens.md` — its blue `#1D4ED8` palette, radius policy, elevation policy, card policy, and shell dimensions are superseded by the design language v2. It retains authority only for accessibility method, non-colour encoding, chart data-quality states, and token-pipeline format.
- `docs/03-design-architecture/iip_visual_direction_exploration.md` and `iip_visual_direction_review.md` — the direction question they left open is now decided. Their accessibility findings are carried forward.
- The navigation-layer and shell-chrome content of `docs/03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md`. That document still owns route IDs, the object model, and permission-derived visibility.
