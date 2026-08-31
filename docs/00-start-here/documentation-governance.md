# Documentation governance

Use these rules to keep IIP knowledge findable and prevent competing sources of truth.

## Organization scheme

IIP uses a hybrid scheme:

1. **Task/lifecycle** for the numbered documentation pack: strategy → engineering/design → quality → deployment → pilot/support → improvement/commercial.
2. **Artifact type** for cross-cutting areas: decisions, API contracts, QA evidence, research, and agent coordination.
3. **Progressive disclosure** for navigation: [`../README.md`](../README.md) → this start-here layer → owning documents → [`../CATALOG.md`](../CATALOG.md).

Do not infer authority from a lower folder number. Authority comes from [`../../AGENTS.md`](../../AGENTS.md), current implementation evidence, and the owning approved document or ADR.

## Lifecycle labels

Every new document that could be mistaken for current guidance should state one of these near the title:

| State | Meaning |
|---|---|
| `Proposed` | Under review; not an approved contract |
| `Active` | Current approved guidance for its owned concern |
| `Evidence` | Dated proof of an observed run or decision; not reusable as a standing contract |
| `Template` | Blank reusable structure; contains no completion evidence |
| `Historical` | Preserved context that must not guide new implementation |
| `Superseded` | Replaced; must link to the active replacement |

Recommended header:

```markdown
> Status: Active
> Owner: Platform Engineering
> Last reviewed: YYYY-MM-DD
> Replaces: path/to/older-document.md
```

Use `Replaces` only when applicable. Do not fabricate an owner or review date.

## Placement and naming

- Place a document in the folder that owns its primary maintenance task.
- Prefer lowercase kebab-case for new Markdown filenames. Preserve established external or generated contract names.
- Date evidence and snapshots as `yyyy-mm-dd`; do not date durable specifications unless the date is part of their identity.
- Keep API machine contracts in `docs/api/`; keep explanatory API guidance with development engineering.
- Keep research evidence and synthesis in `docs/research/`; move adopted decisions into the owning specification or ADR.
- Keep temporary reasoning in `.scratch/`; promote only reviewed outcomes into `docs/`.
- Store generated test output outside source control unless it is curated release evidence with an owner and retention purpose.

## Change rules

When adding or changing documentation:

1. Identify the owner, user task, lifecycle state, and authority level.
2. Search for an existing document that owns the concern.
3. Update that document when possible instead of creating a competing one.
4. Add cross-links from the task-oriented index when the document is a common entry point.
5. Mark replaced content `Superseded`; do not silently leave both active.
6. Regenerate [`../CATALOG.md`](../CATALOG.md):

   ```powershell
   pwsh scripts/docs/Update-IipDocsCatalog.ps1
   ```

7. Check relative links and review the diff before committing.

## Archival and deletion

- Archive only when the document has continuing historical, legal, audit, or decision value.
- Delete only when it is generated, duplicated without unique evidence, or explicitly superseded with no retention need.
- Before moving or deleting, find inbound links, automation references, and external publication dependencies.
- Prefer a small move batch with link repair and validation over a repository-wide rename.
- Never treat `.scratch/`, local archives, or generated catalogs as product authority.

## Governance risks

- Duplicate ordinals in the current documentation pack reduce visual predictability but are safer to retain until inbound links are mapped.
- Many implementation-output documents can look authoritative after code changes; they need lifecycle labels during normal touch work.
- Dated QA evidence must not be cited as proof of current readiness without a fresh run.
- Root guidance must stay short; volatile inventories belong to generated catalogs or owning documents.
