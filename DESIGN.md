# DESIGN.md — UI and UX authority for IIP

Read this before changing anything a user can see. It is the entry point; the four documents it links are the detail. `AGENTS.md` invariants outrank everything here — a visual rule never justifies weakening security, tenant isolation, human authority, or honest state.

## The four documents

| Document | Owns | Read it when |
|---|---|---|
| [`docs/03-design-architecture/iip_design_language_v2.md`](docs/03-design-architecture/iip_design_language_v2.md) | Tokens, colour, type, spacing, radius, elevation, motion, shell geometry, ~25 component contracts, migration map | Changing how anything looks |
| [`docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md`](docs/03-design-architecture/iip_ux_cognitive_load_and_navigation_model.md) | Navigation model, the chat-first landing, chat-to-work handoff, progressive disclosure, toast and interruption policy, forms | Adding a screen, a destination, or any notification |
| [`docs/03-design-architecture/iip_workflow_legibility_contract.md`](docs/03-design-architecture/iip_workflow_legibility_contract.md) | The claim lifecycle as rendered: stage map, completeness display, evidence and confidence, the human-decision boundary, the product-wide status vocabulary | Rendering a claim, work item, document, or agent output |
| [`docs/03-design-architecture/iip_ui_craft_guardrails.md`](docs/03-design-architecture/iip_ui_craft_guardrails.md) | The craft floor: banned patterns, the emoji ban, copy rules, the 27-point pre-merge checklist, automated enforcement | Before opening any UI pull request |

Visual reference for the language: [`docs/03-design-architecture/reference/bevaya-design-language.md`](docs/03-design-architecture/reference/bevaya-design-language.md) and [`docs/03-design-architecture/reference/bevaya-ui-replica.html`](docs/03-design-architecture/reference/bevaya-ui-replica.html). Open the HTML in a browser to see the target. Copy its visual grammar; do not copy its product scope, and do not copy its emoji.

## The direction, in short

A calm, border-led, light-first insurance operations console. One violet accent (`#5B2AE8`) used only to mark the next action. Inter throughout. Hairline borders instead of shadows. Colour rationed to 6px status dots and single figures. A 64px icon rail, a contextual panel, and a left-aligned content column. It should read as **a precise instrument, not a product tour.**

## The ten rules that are never waived

1. **No emoji anywhere** — UI, code, chat output, commit messages, docs. Guardrails §3.
2. **One violet primary action per screen region.** Violet marks what to do next; it never decorates.
3. **Gradients in exactly two places** — the Assistant orb and chart bars. Nowhere else.
4. **Borders, not shadows.** Static cards carry `box-shadow: none`. Shadow is only for genuinely floating surfaces.
5. **Status is a neutral pill plus a 6px coloured dot plus a word.** Never colour alone, never a tinted pill, never coloured pill text.
6. **Every screen answers five questions on sight**: where am I, what is this, what needs me, what next, how do I get back. Navigation model §4.
7. **Six states or it is not finished**: loading, empty, empty-after-filter, error, not configured, no permission.
8. **No fabricated data.** No fake numbers, placeholder names, dead buttons, or "coming soon". Missing capability renders `Not configured`.
9. **AI prepares; humans decide** — and the interface shows the difference. Prepared work sits in the AI narrative card; the decision sits in a separate neutral decision bar with an explicit verb and a consequence preview.
10. **Every actionable AI assertion carries a citation** that opens its source. No citation, no render.

## Where things live

- Landing screen is the **Assistant** at `/app` — chat first, with suggestion chips generated from the user's real pending work, and replies that end in action cards which carry context into the real work surfaces.
- The icon rail holds **seven destinations maximum**: Assistant, My work, Claims, Inbox, Documents, Automation, Insights. Everything else nests inside one of them. Navigation model §5.
- Toasts are rare and governed: off-screen, this user's work, actionable now, not already shown. Everything else is a badge, an inline message, or the notification bell. Navigation model §8.

## Before you merge

Run the 27-point checklist in [`iip_ui_craft_guardrails.md`](docs/03-design-architecture/iip_ui_craft_guardrails.md) §8. One "no" blocks the merge. Then run `pnpm lint`, `pnpm build`, `pnpm test` from `web/portal` and report the real results.

## Superseded documents

The following remain in the repository for history. Do not build from their visual guidance.

- `docs/03-design-architecture/iip_visual_design_system_and_tokens.md` — its blue `#1D4ED8` palette, radius policy, elevation policy, card policy, and shell dimensions are superseded by the design language v2. It retains authority only for accessibility method, non-colour encoding, chart data-quality states, and token-pipeline format.
- `docs/03-design-architecture/iip_visual_direction_exploration.md` and `iip_visual_direction_review.md` — the direction question they left open is now decided. Their accessibility findings are carried forward.
- The navigation-layer and shell-chrome content of `docs/03-design-architecture/iip_information_architecture_navigation_and_screen_inventory.md`. That document still owns route IDs, the object model, and permission-derived visibility.
