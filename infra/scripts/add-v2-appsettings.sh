#!/usr/bin/env bash
#
# add-v2-appsettings.sh — SAFE, ADDITIVE. The existing staging App Services already carry the
# runtime app settings from the prior deployment (per the Azure Resource Inventory: "No action
# needed unless you rotate a secret"). v2 only introduces a few NEW settings that the old repo did
# not have. This script sets ONLY those, so it will not clobber the existing provider/Entra/SQL
# configuration. `az ... appsettings set --settings` updates only the named keys.
#
# Run as an authenticated `az` user with rights on rg-iip-stg. Sets app settings only — creates
# nothing, touches no Partner Center / Marketplace asset. No secrets in this file.
#
# For a FRESH App Service (or to reconcile full drift) use set-staging-appsettings.sh instead, after
# reviewing the existing values with verify-staging-appsettings.sh.
set -euo pipefail

RG="${RG:-rg-iip-stg}"
API_APP="${API_APP:-app-api-stg-cus-vyhhymwydvai2}"
PORTAL_APP="${PORTAL_APP:-app-portal-stg-cus-vyhhymwydvai2}"
KV_NAME="${KV_NAME:-kv-stg-vyhhymwydvai2}"

echo "Adding v2-only settings to $API_APP (existing settings are left untouched)…"
az webapp config appsettings set --resource-group "$RG" --name "$API_APP" --settings \
  Azure__KeyVaultUri="https://${KV_NAME}.vault.azure.net/" \
  Onboarding__AdminConsentRedirectUri="https://${PORTAL_APP}.azurewebsites.net/onboarding/consent-callback" \
  1>/dev/null
echo "  Added: Azure__KeyVaultUri (G1.6 Key Vault config provider), Onboarding__AdminConsentRedirectUri (G1.7)."

echo
echo "Verify (does NOT change anything) that these are already present and correct on $API_APP:"
echo "  INSURANCEINTELLIGENCE_AUTH_MODE=Entra"
echo "  INSURANCEINTELLIGENCE_RUNTIME_PROFILE=Staging"
echo "  INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID / _ENTRA_AUDIENCE (existing app registration)"
echo "  provider modes + endpoints (Blob/DocIntel/AISearch/OpenAI/ServiceBus) — already live"
echo "Run: bash infra/scripts/verify-staging-appsettings.sh"
