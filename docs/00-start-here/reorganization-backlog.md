# Repository reorganization backlog

This is the reviewed move matrix for structural cleanup that is unsafe to perform as an unrelated bulk change. No item authorizes deletion by itself.

| Current location or pattern | Target or decision | Action now | Preconditions for physical change |
|---|---|---|---|
| Root `CONTEXT.md` product glossary | `docs/00-start-here/product-glossary.md` | Move | Update references; preserve content |
| Root `IIP_Docs_1-9_Complete.zip` | `artifacts/documentation-archives/` | Move locally | Confirm destination; do not commit generated archive by default |
| `.nuget-packages/` | Local dependency cache | Ignore | None; never treat as source |
| `tmp/` | Local temporary work | Ignore | Confirm no governed evidence is stored there |
| `docs/CONTEXT.md` | Current-context snapshot or historical handoff | Defer | Revalidate every status claim and choose lifecycle label before rename |
| Duplicate `06-*`, `07-*`, and `08-*` ordinals | Unique stable taxonomy | Defer | Build inbound-link map; agree permanent labels; update links atomically |
| `docs/02-development-engineering/*implementation_output*` | Active guide, evidence, or historical archive | Defer | Compare each file with code/tests and assign lifecycle/owner |
| `.scratch/` remediation material | Issue tracker, governed evidence, or disposal | Defer | Close active work; identify unique evidence and retention owner |
| `artifacts/` and generated QA output | Explicit retention layout | Defer | Define artifact owner, retention, publication, and source-control policy |
| Root `.agents/`, `.claude/`, and `skills-lock.json` | Repository-scoped agent tooling | Defer | Confirm which tools consume each path and commit them as one coherent change |

## Safe batch rule

For each deferred batch: inventory → identify owner and inbound references → define destination → move with history → repair links/automation → regenerate the catalog → run affected validation → review before commit.
