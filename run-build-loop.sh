#!/usr/bin/env bash
# Runs the IIP build loop: starts Claude Code repeatedly with the same prompt.
# Each run reads docs/build-loop/BUILD_PLAN.md, does the next item, commits, and exits or continues.
# The loop ends when no TODO or IN PROGRESS items are left, or after MAX_RUNS.
#
# Usage (from the IIP repo root, with IIP_BUILD_LOOP_PROMPT.md and IIP_AUDIT.md in the repo root):
#   ./run-build-loop.sh
#
# Choose a permission mode you are comfortable with for unattended runs. The prompt keeps all work
# in a separate git worktree on branch build/claims-core and forbids live credentials and deploys.

set -u
MAX_RUNS="${MAX_RUNS:-60}"
PLAN="../iip-build/docs/build-loop/BUILD_PLAN.md"
PROMPT_FILE="IIP_BUILD_LOOP_PROMPT.md"

# Create the separate worktree and branch once, so the current working tree is never touched.
if [ ! -d ../iip-build ]; then
  git fetch origin
  git worktree add ../iip-build -b build/claims-core origin/main || exit 1
fi
cp -n IIP_AUDIT.md IIP_BUILD_LOOP_PROMPT.md ../iip-build/ 2>/dev/null || true

for i in $(seq 1 "$MAX_RUNS"); do
  echo "=== Build loop run $i of $MAX_RUNS ($(date)) ==="
  # acceptEdits lets file edits through; --allowedTools lets the build/test/git commands run;
  # --permission-prompts none denies anything else instead of hanging an unattended run.
  claude -p "$(cat "$PROMPT_FILE")" \
    --permission-mode acceptEdits \
    --permission-prompts none \
    --add-dir ../iip-build \
    --allowedTools "Bash(git *)" "Bash(dotnet *)" "Bash(pnpm *)" "Bash(npx *)" "Bash(node *)" \
                   "Bash(cd *)" "Bash(ls *)" "Bash(mkdir *)" "Bash(cp *)" "Bash(bash scripts/*)" \
                   "Bash(curl http://localhost*)" \
    | tee -a build-loop.log

  if [ -f "$PLAN" ] && ! grep -Eq '\b(TODO|IN PROGRESS)\b' "$PLAN"; then
    echo "No TODO or IN PROGRESS items left. Loop finished."
    break
  fi
  sleep 5
done
