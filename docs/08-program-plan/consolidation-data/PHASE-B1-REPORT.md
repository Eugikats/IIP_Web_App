# Phase B1 report: SaaS Academy snapshot

Date: 2026-11-03. Status: IN PROGRESS. The Academy-specific gate passed. The section 8 global gate remains open, so `main` has not advanced beyond `80155dc`.

## Source and branch

- Repository: `Otic-Group/IIP_Web_App`.
- Execution clone: `C:/work/iip-consolidation-verified`.
- Phase branch: `integ/01-saas-academy`.
- Baseline: `80155dc` (`fix(portal): clarify unavailable claim recovery state`).
- SaaS Academy source snapshot: `8e8d395` (`wip(saas): Academy media endpoints, lesson media, assessments, pricing and payment changes`).
- Owner setup document: `7673a1c` (`docs(academy): owner setup notes for Google and Microsoft`).
- B1 follow-up code and browser-check fixes: `285951d` (`fix(saas): clarify Academy media readiness and stabilize portal checks`).

## Changes and conflict resolution

The Academy snapshot was cherry-picked onto the live baseline. No cherry-pick conflicts occurred. The owner setup document was then cherry-picked as its own documentation commit. No migration was added or changed.

The follow-up commit makes lesson media report `Not configured` when blob storage is absent, while preserving retry behavior for transient Azure errors. The portal parses the API's safe detail and renders the explicit unavailable state. Browser tests now wait for route changes instead of racing them, use current Academy pricing and enrollment behavior, and cover unavailable media.

The workspace renewal test now uses the selected insurer tenant and workspace when checking policy-date event provenance. It verifies that insurers do not receive the bank-only events tab. The Next.js development badge is disabled because it covered the profile menu at tablet width; Next's full development error overlay remains enabled. The rationale and evidence are recorded as D-014.

## Verification

All test, build, and commit commands were preceded by `scripts/guard/check-config-injection.sh`; each observed run exited 0.

| Check | Result |
|---|---|
| Academy API tests | 59 passed, 0 failed |
| Academy browser matrix | 30 passed of 30 across phone, desktop, and tablet |
| Workspace browser matrix | 140 passed of 140 across desktop and tablet, 11.5 minutes |
| Portal lint | Exit 0, no errors, 7 existing `<img>` advisory warnings |
| Portal `pnpm test` | Passed: runtime data, provider status, role routes, action permission parity, UI release gate, trust/truth 3/3, authority/permissions 4/4 |
| Portal production build | Exit 0, 76 static pages generated. Google Fonts timed out during download and Next used its fallback. Seven existing image advisories and an old Browserslist database warning remain. |
| API Release build and Academy tests | Previously passed with 0 warnings and 0 errors, and 59 Academy tests passed. No API source changed after that run. |
| Runtime recovery browser check | 1 passed |

## Global gate still open

The last broad visual run reported 95 passed, 54 failed, and 49 skipped. Its failures include stale route, greeting, label, and fixture expectations; accessibility contrast and landmark findings; and a Knowledge Manager client exception. The visual suite has not been rerun after the B1 fixes, so those counts are prior evidence and the gate remains unproven.

The last deployed DOM run reported 1 passed, 2 failed, and 3 skipped. It has not been rerun after the B1 fixes. The full section 8 gate is not green, so the phase branch is not eligible to advance `main`.

Azure Blob media playback was not verified because no media account or local assets are configured. The application and browser check report that capability honestly as `Not configured`; this is not reported as a streaming pass.

## Main and release status

- `origin/main` remains at `80155dc`.
- Nothing was deployed, and no cloud resource or database was changed.
- This phase branch is reviewable but remains in progress until the global visual and deployed checks, plus the other required section 8 checks, pass.

## Next work

Continue with B2 on a branch stacked above this B1 snapshot while keeping `main` unchanged. Record the branch-order deviation in D-015. Finish the remaining phase work, then run and clear the complete section 8 gate before the single final fast-forward of `main`.
