#!/usr/bin/env bash
# Starts IIP locally with every external provider set to its deterministic Mock adapter, seeded
# synthetic data, Development auth (header identity) and in-memory storage. No Azure, Meta, Graph,
# SMS or payment credentials are used or needed, and nothing leaves this machine.
#
#   scripts/run-local-mock.sh            start the API (5167) and the portal (3000)
#   scripts/run-local-mock.sh --api-only start only the API
#
# Logs: .local/run-local-mock/api.log and portal.log. Stop with Ctrl+C.
# Seeded scope: tenant "tenant-demo", workspace "workspace-operations", user "local-admin".
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="$ROOT/.local/run-local-mock"
API_URL="${IIP_API_URL:-http://localhost:5167}"
mkdir -p "$LOG_DIR"

# Mock modes are honoured only under a developer runtime. Queue and notification providers are
# forced to Mock because appsettings.Development.json points them at a real Service Bus namespace.
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="$API_URL"
export INSURANCEINTELLIGENCE_AUTH_MODE=Development
export INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory
export INSURANCEINTELLIGENCE_BLOB_PROVIDER=Mock
export INSURANCEINTELLIGENCE_AI_PROVIDER=Mock
export INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER=Mock
export INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER=Mock
export INSURANCEINTELLIGENCE_QUEUE_PROVIDER=Mock
export INSURANCEINTELLIGENCE_EMAIL_PROVIDER=Mock
export INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER=Mock
export INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=Mock
export INSURANCEINTELLIGENCE_SMS_PROVIDER=Mock
export INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true
export INSURANCEINTELLIGENCE_LOCAL_DOCUMENT_STORAGE_PATH="$LOG_DIR/storage"

cleanup() {
  [ -n "${API_PID:-}" ] && kill "$API_PID" 2>/dev/null || true
  [ -n "${PORTAL_PID:-}" ] && kill "$PORTAL_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "Starting API on $API_URL (mock providers)..."
dotnet run --no-launch-profile --project "$ROOT/src/InsuranceIntelligence.Api/InsuranceIntelligence.Api.csproj" -c Release \
  >"$LOG_DIR/api.log" 2>&1 &
API_PID=$!

for _ in $(seq 1 90); do
  if curl -fsS "$API_URL/health" >/dev/null 2>&1; then
    echo "API is up. Provider status: $API_URL/api/v1/provider-status"
    break
  fi
  sleep 2
done
curl -fsS "$API_URL/health" >/dev/null 2>&1 || { echo "API did not start; see $LOG_DIR/api.log"; exit 1; }

if [ "${1:-}" = "--api-only" ]; then
  echo "API only. Press Ctrl+C to stop."
  wait "$API_PID"
  exit 0
fi

echo "Starting portal on http://localhost:3000 ..."
(
  cd "$ROOT/web/portal"
  NEXT_PUBLIC_API_BASE_URL="" \
  NEXT_PUBLIC_TENANT_ID=tenant-demo \
  NEXT_PUBLIC_WORKSPACE_ID=workspace-operations \
  NEXT_PUBLIC_LOCAL_USER_ID=local-admin \
  NEXT_PUBLIC_AUTH_MODE=Development \
    pnpm dev >"$LOG_DIR/portal.log" 2>&1
) &
PORTAL_PID=$!

echo "Portal starting (first compile takes about a minute). Open http://localhost:3000"
echo "Press Ctrl+C to stop both."
wait
