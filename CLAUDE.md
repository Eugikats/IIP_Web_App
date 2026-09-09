# Claude execution instructions

Read and follow [`AGENTS.md`](AGENTS.md) before acting. Its **Authority and current truth** section is the canonical repository authority index; this file adds only Claude-specific working behavior.

## Before changing files

1. Inspect `git status` and preserve unrelated user changes.
2. Read the explicit task, current implementation and the owning docs/ADR identified by `AGENTS.md`.
3. Resolve material source conflicts instead of merging them or falling back to an older path.
4. Identify the supported path, legacy path, migration/removal work and verification command.

## Before changing anything a user can see

Read [`DESIGN.md`](DESIGN.md) first. It is the UI/UX authority index and links the four owning documents. Do not design from memory, from the current portal's appearance, or from a screenshot in a prompt — the current portal is the thing being replaced.

The direction: a calm, border-led, light-first insurance operations console. One violet accent (`#5B2AE8`) marking only the next action, Inter throughout, hairline borders instead of shadows, colour rationed to 6px status dots and single figures, a 64px icon rail with a contextual panel and a left-aligned content column. The visual reference is `docs/03-design-architecture/reference/bevaya-ui-replica.html` — open it in a browser. Copy its visual grammar; do not copy its product scope or its emoji.

Rules that are never waived when writing UI:

- No emoji anywhere — UI, code, model output rendered to users, commit messages, docs.
- No gradients outside the two named uses; no glassmorphism, glow, hover-lift, or decorative motion.
- One violet primary action per screen region. Status is a neutral pill plus a coloured dot plus a word, never colour alone.
- Six states or it is not finished: loading, empty, empty-after-filter, error, not configured, no permission.
- No fabricated data, dead controls, placeholder names, or unshipped features. Absent capability renders `Not configured`.
- AI preparation and human decision authority live in separate containers; every actionable AI assertion carries a citation that opens its source.
- Workflow state and its label come from the vocabulary table in the workflow legibility contract, which is derived from the Domain enums.

Clear the 27-point checklist in `docs/03-design-architecture/iip_ui_craft_guardrails.md` §8 before reporting a UI change complete, and actually open the screen in a browser at 1280px and 768px rather than inferring how it looks.

## While working

- Implement the smallest coherent forward change. Do not add a competing architecture, hidden fallback or unrelated cleanup.
- Keep business rules, tenant/permission enforcement, human gates and audit requirements in Domain/Application modules; keep framework/provider details in adapters.
- Characterize uncertain behavior before changing it, but do not preserve obsolete or harmful behavior without an active requirement.
- Update tests and owning documentation with contract changes.
- Never weaken protected-runtime validation, security, tenant isolation, evidence grounding or human authority to make a check pass.
- Use current code, CI and owning documents for routes, UI, providers, deployment and release truth; do not rely on catalogs copied into prompts or historical files.

## Validation and completion

- Run targeted checks during iteration and the relevant verified commands from `AGENTS.md`/CI before completion when feasible.
- Report actual commands and outcomes. State skipped, unavailable or manual validation explicitly.
- Distinguish source-complete, locally verified, deployed, staging-validated, production-candidate and marketplace readiness.
- Summarize files/behavior changed, legacy paths removed or time-bounded, safety/data/UI/operational impact, tests, blockers and manual actions.
- Do not deploy, merge, push, modify cloud resources, apply production migrations or publish artifacts unless explicitly requested.

## Deploying to Azure

When explicitly asked to push/deploy to Azure, read [`docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`](docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md) first — it is the authoritative index of which subscriptions/resource groups actually hold IIP resources, who has access, and the verified deploy procedure. Do not guess resource names or assume `deploy-staging.yml` targets a live environment — confirm live via `az` before deploying, the same way that document was produced, since Azure resources here have been deleted/renamed out-of-band before.
