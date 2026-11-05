#!/usr/bin/env bash
# Fails if a tracked or untracked source/config file shows the hidden-payload injection pattern seen in
# web/portal/postcss.config.js (2026): a normal-looking file followed by a very long line, usually after a long run
# of tabs or spaces, or the helper files that campaign used. Read-only. Run before every install, build and commit.
# Usage: scripts/guard/check-config-injection.sh [repo-root]   Exit 0 = clean, 1 = suspicious.
set -u
cd "${1:-.}" || exit 2
git rev-parse --show-toplevel >/dev/null || exit 2
fail=0
configs='(^|/)(postcss|tailwind|next|eslint|vite|webpack|babel|jest|playwright|prettier)\.config\.(js|cjs|mjs|ts)$|(^|/)\.eslintrc(\.(js|cjs))?$'

# 1. Config files: no line over 400 chars, no long hidden whitespace run followed by code.
for f in $(git ls-files -co --exclude-standard | grep -E "$configs"); do
  [ -f "$f" ] || continue
  if git grep --untracked -qE '.{401,}' -- "$f" 2>/dev/null; then echo "SUSPICIOUS long line in config file: $f"; fail=1; fi
  if git grep --untracked -qP '(\t{12,}| {60,})\S' -- "$f" 2>/dev/null; then echo "SUSPICIOUS hidden whitespace run in config file: $f"; fail=1; fi
done

# 2. Source files: no line over 3000 chars except known data files.
allow='^web/portal/lovable-pages/OperationalIntelligencePrototype\.tsx$'
for f in $(git grep --untracked -lE '.{3001,}' -- '*.js' '*.mjs' '*.cjs' '*.ts' '*.tsx' '*.jsx' '*.cs' '*.ps1' '*.sh' '*.bat' '*.cmd' '*.yml' '*.yaml' 'web/portal/init' ':!**/node_modules/**' ':!**/.next/**' 2>/dev/null); do
  echo "$f" | grep -qE "$allow" && continue
  echo "SUSPICIOUS very long line (>3000 chars): $f"; fail=1
done

# 3. Helper files and ignore entries from the campaign.
for n in temp_auto_push.bat temp_interactive_push.bat branch_structure.json; do
  [ -f "$n" ] && { echo "SUSPICIOUS helper file present: $n"; fail=1; }
  [ -n "$(git ls-files -c -- "$n")" ] && { echo "SUSPICIOUS tracked helper file: $n"; fail=1; }
  grep -qsx "$n" .gitignore && { echo "SUSPICIOUS .gitignore entry hiding: $n"; fail=1; }
done

if [ "$fail" -eq 0 ]; then echo "config injection guard: clean"; else echo "config injection guard: FAILED"; fi
exit $fail
