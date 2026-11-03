# Phase 0 report: prepare the live baseline

Date: 2026-11-03. Status: IN PROGRESS. Main has not been created or advanced.

## Source and authorization

- Repository: `https://github.com/Otic-Group/IIP_Web_App.git`.
- Independent execution clone: `C:/work/iip-consolidation-verified`.
- Phase branch: `integ/00-baseline`.
- Exact live baseline: `4818bb11c125f723f361c89c9100e6b1f839cb76`.
- Plan revision read: `ca93cedbca0bf8eab06b7086e00a6fa98c2fd045`.
- The owner confirmed completion of the listed machine checks and credential rotation in this chat. The agent did not independently certify those owner actions.
- No Azure or database access, deployment, force-push, branch deletion or history rewrite.

## Commits made

| Commit | Subject |
|---|---|
| `18a6fd7abdfe586a61df96b81e71b034959e4e1d` | `chore(security): guard against hidden code injection in config files` |
| `4bc79941b59cc1382428b8283db7ebaa71d681c0` | `fix(build): restore a warning-free consolidation baseline` |

The report commit is identified by the branch history; it cannot embed its own hash.

## Changes and conflicts

No merge or cherry-pick conflicts occurred in this phase. The branch starts directly at the live revision.

- `.github/workflows/ci.yml`: retained all three jobs and their commands, adding the security guard after checkout and before dependency installation, builds and tests.
- `.githooks/pre-commit`: executable hook runs the guard from the repository root. Local `core.hooksPath` is `.githooks`.
- `scripts/guard/check-config-injection.sh`: copied unchanged from the reviewed plan branch. It passed against both the plan tree and live baseline.
- Plan, handover, audit and consolidation data: copied by explicit paths from the plan branch. The migration inventory's extra trailing blank line was removed to pass whitespace validation; no entry changed.
- `src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj`: XML security dependency 8.0.4 to 8.0.5 to resolve four high-severity NuGet warnings.
- `src/InsuranceIntelligence.Api/Auth/ApiKeyAuthentication.cs`: explicit `new` on the existing scheme constant, preserving its name and value.
- `src/InsuranceIntelligence.Application/Rules/OutcomeService.cs`: explicit null guard in the existing inactive-reason branch, preserving behavior.
- `tests/InsuranceIntelligence.Api.Tests/AcademyZenithTests.cs`: equivalent collection assertions recommended by the xUnit analyzer.
- `tests/InsuranceIntelligence.Api.Tests/DocumentUnderstandingTests.cs`: seven existing tests await provider tasks rather than blocking on `.Result`; all assertions retained.
- `tests/InsuranceIntelligence.Api.Tests/RuleEngineTests.cs`: explicit nullable string sequence comparison, retaining both expected template names and order.
- No API route, permission, tenant boundary, human decision gate, persistence contract or migration was replaced.

## Commands and results so far

Each install, build, test invocation and commit was preceded by the injection guard. On Windows the `bash` executable is `C:/Program Files/Git/bin/bash.exe`. Both commits also passed the pre-commit hook.

| Command/check | Result |
|---|---|
| Ordinary fresh clone | Connection reset; unsuccessful |
| Retry of ordinary clone | Stalled; cancelled without checking out source |
| Fresh clone with two local `--reference-if-able` paths and `--dissociate` | Passed; no alternates file remains |
| `git fetch --all` | Passed |
| Guard on plan and live baseline | Exit 0, clean |
| `git cat-file -s HEAD:web/portal/postcss.config.js` at baseline | 81 bytes |
| `dotnet restore InsuranceIntelligence.sln` before fixes | Succeeded, four NU1903 warnings for XML 8.0.4 |
| `dotnet build InsuranceIntelligence.sln --no-restore -c Release -v minimal` before fixes | 0 errors, 17 warnings; did not meet the zero-warning gate |
| `dotnet test InsuranceIntelligence.sln --no-build -c Release` before fixes | Domain: 735 passed; API: 1,741 passed; 0 failed, 0 skipped |
| `dotnet build InsuranceIntelligence.sln -c Release -v minimal` after fixes | 0 errors, 0 warnings |
| `dotnet test InsuranceIntelligence.sln --no-build -c Release` after fixes | Domain: 735 passed; API: 1,741 passed; 0 failed, 0 skipped |
| `pnpm install --frozen-lockfile` | Passed; lockfile unchanged. pnpm reported its existing policy of not running the core-js install script |
| `pnpm lint` | Exit 0; seven existing image-element advisory warnings, no lint errors |
| `pnpm test` | All five static checks passed; trust/truth 3 passed; authority/permissions 4 passed |
| Direct local `bicep build <file> --stdout` for all five CI templates | All exited 0; existing BCP318 warning in `infra/mainTemplate.bicep:1054` |
| Migration source diff from `4818bb1` | Empty; 42 migration files retained unchanged |

The five compiled templates are `infra/bicep/phase3-dev-foundation.bicep`, `infra/bicep/phase6-functions-runtime.bicep`, `infra/mainTemplate.bicep`, `infra/modules/compute/functionapp.bicep` and `infra/modules/messaging/servicebus.bicep`. The installed Bicep binary was invoked directly, with no Azure CLI session or cloud operation.

Backend TRX evidence is in `C:/work/iip-consolidation-evidence/backend-baseline` and `C:/work/iip-consolidation-evidence/backend-fixed`. Both runs used the same set of 2,476 tests. None was deleted, skipped or weakened.

## Pending before phase completion

- Portal production build and browser suites: visual, recovery, deployed DOM, workspace, Academy and sign-in.
- The claims-core journey and Item 37 suite are not present in the live baseline. They arrive in Phase C and have not been claimed as run here.
- Baseline tag, phase push, main creation and remote verification.
- Final phase report update with complete results and any additional fixes.

## Decisions and deviations

See D-006 through D-011 in `DECISIONS.md`: owner prerequisite confirmation, independent clone optimization, guard bootstrap, evidence preservation, baseline warning repairs and the task-specific direct-push authorization. The only application change so far is a patched dependency and behavior-preserving compiler fixes required by the zero-warning gate. The existing live migration history is assumed, as directed; no owner-supplied database-history file is present.
