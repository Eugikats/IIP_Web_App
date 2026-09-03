#!/usr/bin/env bash
# Deploys the IIP API to the dev App Service (app-api-dev-h5girbnzrayqe).
#
# Uses the only packaging path proven to work against Linux App Service:
#   * publish with an explicit linux-x64 RID   — a portable publish fails at runtime with
#                                                "Cannot load Microsoft.Data.SqlClient"
#   * zip with Python                          — .NET/PowerShell zippers write backslash
#                                                separators that Linux cannot extract
#
# Requires: az login, .NET 8 SDK, Python 3.
set -euo pipefail

SUB="${IIP_SUBSCRIPTION:-5f5765ae-985f-42e4-a443-8e2447be33ca}"
RG="${IIP_RESOURCE_GROUP:-rg-iip-dev}"
APP="${IIP_API_APP:-app-api-dev-h5girbnzrayqe}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "==> Building and testing"
dotnet build "$ROOT/InsuranceIntelligence.sln" -c Release --nologo -v q
dotnet test  "$ROOT/InsuranceIntelligence.sln" -c Release --no-build --nologo -v q

echo "==> Publishing (linux-x64)"
dotnet publish "$ROOT/src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj" \
  -c Release -r linux-x64 --self-contained false -o "$WORK/publish" --nologo -v q

echo "==> Packaging"
python - "$WORK/publish" "$WORK/api.zip" <<'PY'
import os, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for root, _, files in os.walk(src):
        for f in files:
            full = os.path.join(root, f)
            z.write(full, os.path.relpath(full, src).replace(os.sep, "/"))
PY

echo "==> Deploying to $APP"
az webapp deploy -n "$APP" -g "$RG" --subscription "$SUB" --src-path "$WORK/api.zip" --type zip -o none

echo "==> Restarting"
az webapp restart -n "$APP" -g "$RG" --subscription "$SUB" -o none

echo "==> Waiting for health (cold start can take ~90s)"
for i in $(seq 1 30); do
  if curl -fsS -m 15 "https://$APP.azurewebsites.net/health/ready" >/dev/null 2>&1; then
    echo "Healthy after ${i} attempt(s):"
    curl -sS -m 20 "https://$APP.azurewebsites.net/health/ready"; echo
    echo
    echo "If this release added EF migrations, apply them now:"
    echo "  TOKEN=\$(az account get-access-token --resource api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73 --query accessToken -o tsv)"
    echo "  curl -X POST -H \"Authorization: Bearer \$TOKEN\" https://$APP.azurewebsites.net/api/v1/admin/db-migrate"
    exit 0
  fi
  sleep 10
done

echo "ERROR: app did not become healthy. Check logs:" >&2
echo "  az webapp log tail -n $APP -g $RG --subscription $SUB" >&2
exit 1
