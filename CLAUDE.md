# Claude execution instructions

Read and follow [`AGENTS.md`](AGENTS.md) before acting. Its **Authority and current truth** section is the canonical repository authority index; this file adds only Claude-specific working behavior.

## Before changing files

1. Inspect `git status` and preserve unrelated user changes.
2. Read the explicit task, current implementation and the owning docs/ADR identified by `AGENTS.md`.
3. Resolve material source conflicts instead of merging them or falling back to an older path.
4. Identify the supported path, legacy path, migration/removal work and verification command.

## Before changing anything a user can see

Read [`DESIGN.md`](DESIGN.md) first. It is the UI/UX authority index and links the four owning documents. Do not design from memory, from the current portal's appearance, or from a screenshot in a prompt — the current portal is the thing being replaced.

The direction: a calm, border-led, light-first insurance operations console. One blue accent (`#1769FF`, set 2026-10-07 in `DESIGN.md`) marking only the next action, Inter throughout, hairline borders instead of shadows, colour rationed to 6px status dots and single figures, a 64px icon rail with a contextual panel and a left-aligned content column. The visual reference is `docs/03-design-architecture/reference/bevaya-ui-replica.html` — open it in a browser. Copy its visual grammar; do not copy its product scope or its emoji.

Rules that are never waived when writing UI:

- No emoji anywhere — UI, code, model output rendered to users, commit messages, docs.
- No em dashes anywhere: UI copy, code, comments, model output shown to users, commit messages and docs. Use a comma, colon, full stop or parentheses instead, and do not substitute a double hyphen. Readers treat the em dash as the plainest sign that text was machine-written. Model completions are cleaned at the provider boundary (`DisplayText.WithoutEmDashes`); everything a person or an agent writes into the repository must follow the rule by hand.
- No gradients outside the two named uses; no glassmorphism, glow, hover-lift, or decorative motion.
- One blue primary action per screen region. Status is a neutral pill plus a coloured dot plus a word, never colour alone.
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

## Where work goes: main first

There is no DevOps pipeline, so a push to `main` is not destructive, and feature branches that are never reconciled are what caused the 2026 consolidation. So:

1. Put work on `main`. Commit and push to `main` by default (after the checks in "Validation and completion" pass).
2. Do not open a feature branch for a normal feature or fix. Create one only when the owner asks, or when the work genuinely cannot land on `main` yet (a long experiment, a risky migration).
3. When you do put work on a feature branch, tell the owner at once, in the same message: the branch name, why it is not on `main`, and what it takes to merge it. Never leave a branch unreported.
4. Before ending a task, check that no branch of yours holds work that `main` lacks. Merge it, or report it.
5. A port or merge must never rewrite a whole file from one lineage over another. Compare the result with the live lineage (`git diff --numstat <live> HEAD`) and look at every file whose deletions outweigh its additions: the 2026-11-06 port commits silently removed about 150 routes and the sign-in page this way.

## Two repositories: the organisation original and a personal mirror

Work is kept in two GitHub repositories. Treat them as follows:

| Remote name | Repository | Role |
|---|---|---|
| `org` | `https://github.com/Otic-Group/IIP_Web_App.git` | The original. Source of truth. |
| `mirror` | `https://github.com/Eugikats/IIP_Web_App.git` | The owner's personal mirror. It should only ever receive what is already on `org/main`. |

Rules:

1. **Always pull from the mirror first.** Before pushing, and at the start of any task that will push, run `git fetch org main` and `git fetch mirror main`. The owner must have no commits on the mirror that `org/main` lacks, so check `git log org/main..mirror/main --oneline`. If it prints anything, stop and report those commits to the owner. Do not overwrite or force-push over them, and do not merge them without the owner's say-so.
2. **Push to both.** After the work is on `org/main`, push the same commit to the mirror: `git push mirror <commit>:refs/heads/main`. Never push to the mirror first, and never push anything to the mirror that is not already on `org/main`. A mirror push is a plain fast-forward: never use `--force`.
3. **Report both.** The completion report names the commit each remote now holds. If the mirror push fails or is skipped, say so. Do not claim the mirror is current.
4. If the `mirror` remote is missing in a checkout, add it with `git remote add mirror https://github.com/Eugikats/IIP_Web_App.git`.
5. The mirror needs the full history, and a first push is large (about 200 MB), so run it in the background and wait for it to finish rather than giving up on a timeout.

## Documentation before every commit of a major build

Updating the documentation is part of finishing a major build, not a follow-up. Do it before the commit, in the same commit, without being asked. A major build is a new or changed feature, route or API contract, migration, configuration or environment variable, provider, user-visible surface, security or tenant rule, edition boundary, deployment or release, or a consolidation phase.

1. List what the build changed, then find the owning documents (`docs/README.md` map, the `AGENTS.md` authority index, `DESIGN.md` for UI) and read them against the code. If a document disagrees with the code, the code wins: correct the document.
2. Update the owning documents and ADRs. Mark replaced guidance `Superseded` with a link to its replacement. Set `Last reviewed` only on a document you actually re-checked.
3. Update `docs/00-start-here/iip-handbook.md` whenever a capability, edition boundary (SaaS or Managed Application), environment, or readiness stage changed.
4. If any document was added, moved or removed, run `pwsh scripts/docs/Update-IipDocsCatalog.ps1` and commit the regenerated `docs/CATALOG.md`.
5. If you find stale documentation outside the build's own scope, fix it when the fix is small; otherwise list it in the completion report as documentation drift. Never leave a known mismatch unreported.
6. Stage the documentation with the code by explicit path. The completion report must carry a `Documentation updated:` line naming the files, or `No documentation impact:` with the reason. A major build committed without one of those two lines is not finished.

## Validation and completion

- Run targeted checks during iteration and the relevant verified commands from `AGENTS.md`/CI before completion when feasible.
- Report actual commands and outcomes. State skipped, unavailable or manual validation explicitly.
- Distinguish source-complete, locally verified, deployed, staging-validated, production-candidate and marketplace readiness.
- Summarize files/behavior changed, legacy paths removed or time-bounded, safety/data/UI/operational impact, tests, blockers and manual actions.
- Do not deploy, merge, push, modify cloud resources, apply production migrations or publish artifacts unless explicitly requested.

## Deploying to Azure

When explicitly asked to push/deploy to Azure, read [`docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md`](docs/05-deployment-release-enablement/AZURE_DEPLOYMENT_AND_ENVIRONMENTS.md) first — it is the authoritative index of which subscriptions/resource groups actually hold IIP resources, who has access, and the verified deploy procedure. Do not guess resource names or assume `deploy-staging.yml` targets a live environment — confirm live via `az` before deploying, the same way that document was produced, since Azure resources here have been deleted/renamed out-of-band before.

## Readiness, gaps and channel/agent configuration work

Before working on customer channels (WhatsApp, SMS, email), agent/system-prompt configuration, knowledge-base surfacing, customer acquisition, FNOL, navigation, or anything framed as "closing gaps for the submission/demo", read [`docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md`](docs/01-strategy-requirements-roadmap/IIP_HACKATHON_READINESS_BLUEPRINT.md) first.

It is the current gap index: what is REAL, PARTIAL, STATIC, MISSING or BLOCKED in each area, verified against source rather than against older documentation, with the file paths that prove each claim and a phased implementation plan. It also carries the IRA/Data Protection and Privacy Act research and an honesty register of what must not be claimed about the platform.

Two rules it establishes that apply beyond its own scope:

- Provider *credentials* belong in the per-tenant, encrypted, validated-before-save Connection model, not in startup environment variables. `WhatsAppConfigurationEndpoints` is the reference implementation; secrets stay write-only from the browser.
- A tenant may configure an agent's voice and scope, never its authority. The immutable platform preamble carrying the human-decision boundary is not tenant-editable.

If the blueprint and the code disagree, the code wins — re-verify and correct the document rather than implementing from a stale claim.
