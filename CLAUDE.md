# Claude execution instructions

Read and follow [`AGENTS.md`](AGENTS.md) before acting. Its **Authority and current truth** section is the canonical repository authority index; this file adds only Claude-specific working behavior.

## Before changing files

1. Inspect `git status` and preserve unrelated user changes.
2. Read the explicit task, current implementation and the owning docs/ADR identified by `AGENTS.md`.
3. Resolve material source conflicts instead of merging them or falling back to an older path.
4. Identify the supported path, legacy path, migration/removal work and verification command.

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
