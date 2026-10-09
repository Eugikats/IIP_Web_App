#!/usr/bin/env bash
# Deploys the IIP portal to the dev App Service (app-portal-dev-h5girbnzrayqe).
#
# Companion to deploy-dev-api.sh. NEXT_PUBLIC_* values are baked at BUILD time, so they are set
# here from the values configured on the target App Service — building with different values
# ships a portal that cannot sign in.
#
# Three packaging rules this script exists to enforce, each learned from a failed deploy:
#
#   1. Copy the standalone root with `cp -r .next/standalone/. dst` (the `/.` form). A `*` glob
#      silently skips the hidden .next/ directory, and Next then refuses to boot with
#      "Could not find a production build in the './.next' directory".
#
#   2. Do NOT locate the server with `find -name server.js | head -1`. That matches
#      node_modules/next/server.js (sorted first) and copies Next's package internals over the
#      bundle root, producing a server.js that starts nothing and exits 0.
#
#   3. Do NOT ship pnpm's traced node_modules. It is a .pnpm store plus symlinks; neither cp on
#      Windows nor Python's zipfile preserves those, so the App Service gets a half-dereferenced
#      tree and Next dies on "Cannot find module 'styled-jsx/package.json'". Dependencies are
#      installed flat with npm from the standalone package.json, which is self-contained.
#
# Requires: az login, Node 20 + pnpm + npm, Python 3.
set -euo pipefail

SUB="${IIP_SUBSCRIPTION:-5f5765ae-985f-42e4-a443-8e2447be33ca}"
RG="${IIP_RESOURCE_GROUP:-rg-iip-dev}"
APP="${IIP_PORTAL_APP:-app-portal-dev-h5girbnzrayqe}"
API_BASE="${IIP_API_BASE_URL:-https://app-api-dev-h5girbnzrayqe.azurewebsites.net}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PORTAL="$ROOT/web/portal"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

cd "$PORTAL"

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  echo "==> Building (Entra/Demo profile, matching the target App Service settings)"
  # Local-development-only vars are cleared: baked in, they would make the portal attach demo
  # identity headers instead of using real Entra sign-in.
  env -u NEXT_PUBLIC_TENANT_ID -u NEXT_PUBLIC_WORKSPACE_ID -u NEXT_PUBLIC_LOCAL_USER_ID -u NEXT_PUBLIC_VISUAL_PARITY_MODE \
    NEXT_PUBLIC_API_BASE_URL="$API_BASE" \
    NEXT_PUBLIC_AUTH_MODE="${IIP_AUTH_MODE:-Entra}" \
    NEXT_PUBLIC_RUNTIME_PROFILE="${IIP_RUNTIME_PROFILE:-Demo}" \
    NEXT_PUBLIC_PRESENTATION_MODE="${IIP_PRESENTATION_MODE:-}" \
    NEXT_PUBLIC_ENTRA_TENANT_ID="${IIP_ENTRA_TENANT_ID:-dcb0b132-fb9e-475c-a68e-b0671222e8d8}" \
    NEXT_PUBLIC_ENTRA_CLIENT_ID="${IIP_ENTRA_CLIENT_ID:-3b3522e4-13fd-4bd9-8f08-a1cedf428d0d}" \
    NEXT_PUBLIC_ENTRA_PORTAL_CLIENT_ID="${IIP_ENTRA_CLIENT_ID:-3b3522e4-13fd-4bd9-8f08-a1cedf428d0d}" \
    NEXT_PUBLIC_ENTRA_API_SCOPE="${IIP_ENTRA_API_SCOPE:-api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73/access_as_user}" \
    NEXT_PUBLIC_ENTRA_AUTHORITY_TENANT="${IIP_ENTRA_AUTHORITY_TENANT:-}" \
    NEXT_PUBLIC_EXTERNAL_ID_AUTHORITY="${IIP_EXTERNAL_ID_AUTHORITY:-}" \
    NEXT_PUBLIC_EXTERNAL_ID_CLIENT_ID="${IIP_EXTERNAL_ID_CLIENT_ID:-}" \
    NEXT_PUBLIC_EXTERNAL_ID_API_SCOPE="${IIP_EXTERNAL_ID_API_SCOPE:-}" \
    NEXT_PUBLIC_ENTRA_REDIRECT_URI="${IIP_ENTRA_REDIRECT_URI:-https://app-portal-dev-h5girbnzrayqe.azurewebsites.net/app}" \
    pnpm build
fi

DEPLOY="$WORK/deploy"
echo "==> Assembling bundle"
rm -rf "$DEPLOY" && mkdir -p "$DEPLOY"
cp -r .next/standalone/. "$DEPLOY/"     # rule 1: `/.` copies the hidden .next/ too
rm -rf "$DEPLOY/node_modules"           # rule 3: pnpm's symlink tree is replaced below
mkdir -p "$DEPLOY/.next"
cp -r .next/static "$DEPLOY/.next/static"
[ -d public ] && cp -r public "$DEPLOY/public"

echo "==> Installing production dependencies flat"
( cd "$DEPLOY" && npm install --omit=dev --no-audit --no-fund --ignore-scripts --silent )

echo "==> Verifying the bundle before it goes near the App Service"
fail() { echo "ERROR: $1" >&2; exit 1; }
[ -f "$DEPLOY/server.js" ] || fail "server.js missing from bundle root"
grep -q "currentPort" "$DEPLOY/server.js" || fail "root server.js is not the Next standalone server (rule 2)"
[ -f "$DEPLOY/.next/BUILD_ID" ] || fail ".next/BUILD_ID missing — Next will refuse to start (rule 1)"
[ -d "$DEPLOY/.next/server" ] || fail ".next/server missing — Next will refuse to start (rule 1)"
[ -d "$DEPLOY/.next/static" ] || fail ".next/static missing"
[ -f "$DEPLOY/node_modules/next/package.json" ] || fail "node_modules/next missing"
# npm may hoist styled-jsx to the root or nest it under next/ — both resolve from next.
[ -f "$DEPLOY/node_modules/styled-jsx/package.json" ] || [ -f "$DEPLOY/node_modules/next/node_modules/styled-jsx/package.json" ] \
  || fail "styled-jsx not resolvable from next (rule 3)"
[ -f "$DEPLOY/node_modules/react/package.json" ] || fail "react missing"
[ -e "$DEPLOY/amp.js" ] && fail "Next package internals leaked into the bundle root (rule 2)"
find "$DEPLOY" -type l | head -1 | grep -q . && fail "bundle contains symlinks; they will not survive zipping (rule 3)"
echo "    bundle verified"

echo "==> Packaging"
# Python zip: .NET/PowerShell zippers write backslash separators that Linux cannot extract.
python - "$DEPLOY" "$WORK/portal.zip" <<'PY'
import os, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
n = 0
with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for root, _, files in os.walk(src):
        for f in files:
            full = os.path.join(root, f)
            z.write(full, os.path.relpath(full, src).replace(os.sep, "/"))
            n += 1
print(f"    packed {n} files")
PY
python - "$WORK/portal.zip" <<'PY'
import sys, zipfile
names = set(zipfile.ZipFile(sys.argv[1]).namelist())
for required in ("server.js", ".next/BUILD_ID", "node_modules/next/package.json"):
    assert required in names, f"{required} missing from zip"
assert ("node_modules/styled-jsx/package.json" in names
        or "node_modules/next/node_modules/styled-jsx/package.json" in names), "styled-jsx missing from zip"
for prefix in (".next/static/", ".next/server/"):
    assert any(n.startswith(prefix) for n in names), f"{prefix} missing from zip"
print(f"    zip verified ({len(names)} entries)")
PY

echo "==> Deploying to $APP (clean: wwwroot is replaced, not merged)"
# --clean matters: ZipDeploy merges by default, so a previous bad bundle's files survive.
# The B1 plan is slow enough that the client can report a 504 while the deployment succeeds,
# so the server-side status and a real HTTP check below are what actually decide the outcome.
az webapp deploy -n "$APP" -g "$RG" --subscription "$SUB" \
  --src-path "$WORK/portal.zip" --type zip --clean true --restart true -o none --timeout 1800 || \
  echo "    (deploy client returned non-zero — checking server-side status)"

az webapp log deployment show -n "$APP" -g "$RG" --subscription "$SUB" -o json 2>/dev/null | python -c "
import json,sys
d=json.load(sys.stdin)
if isinstance(d,list): d=d[-1] if d else {}
print('   ', d.get('id'), '|', str(d.get('message','(none)'))[:140])
" || true

az webapp start -n "$APP" -g "$RG" --subscription "$SUB" -o none || true

echo "==> Waiting for the portal to answer (cold start on B1 can take ~90s)"
for i in $(seq 1 40); do
  CODE="$(curl -s -o /dev/null -w '%{http_code}' -m 25 "https://$APP.azurewebsites.net/" || true)"
  if [ "$CODE" = "200" ]; then
    echo "Portal answering 200 after ${i} attempt(s)"
    echo
    echo "NOTE: the API and portal share a single-worker B1 plan. Restarting one can evict the"
    echo "other; if the API stops answering after this, restart it and let both warm up."
    exit 0
  fi
  echo "    attempt $i: HTTP $CODE"
  sleep 15
done

echo "ERROR: portal did not answer 200. Check logs:" >&2
echo "  az webapp log tail -n $APP -g $RG --subscription $SUB" >&2
exit 1
