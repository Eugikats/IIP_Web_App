#!/usr/bin/env bash
#
# verify-staging-appsettings.sh — READ-ONLY. Prints the current app settings on the existing
# staging App Services so you can confirm what is already live before deciding whether to change
# anything. Per the Azure Resource Inventory, the existing App Services already carry working
# runtime settings from the prior deployment; prefer verifying over overwriting. Secret VALUES are
# not printed (Key Vault references show as '@Microsoft.KeyVault(...)', not the secret itself).
#
# Changes nothing. Safe to run anytime as an authenticated `az` user with read on rg-iip-stg.
set -euo pipefail

RG="${RG:-rg-iip-stg}"
API_APP="${API_APP:-app-api-stg-cus-vyhhymwydvai2}"
PORTAL_APP="${PORTAL_APP:-app-portal-stg-cus-vyhhymwydvai2}"

# Setting names v2 depends on. Missing ones are what you need to add (see add-v2-appsettings.sh).
KEYS_OF_INTEREST="INSURANCEINTELLIGENCE_AUTH_MODE INSURANCEINTELLIGENCE_RUNTIME_PROFILE \
INSURANCEINTELLIGENCE_ENTRA_TENANT_ID INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID INSURANCEINTELLIGENCE_ENTRA_AUDIENCE \
INSURANCEINTELLIGENCE_STORAGE_PROVIDER INSURANCEINTELLIGENCE_BLOB_PROVIDER INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER \
INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER INSURANCEINTELLIGENCE_AI_PROVIDER INSURANCEINTELLIGENCE_QUEUE_PROVIDER \
INSURANCEINTELLIGENCE_EMAIL_PROVIDER INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER \
Azure__KeyVaultUri Onboarding__AdminConsentRedirectUri"

echo "== Current app settings on $API_APP (names only; secret values hidden) =="
az webapp config appsettings list --resource-group "$RG" --name "$API_APP" \
  --query "[].name" -o tsv | sort

echo
echo "== v2-required keys: present / MISSING on $API_APP =="
present="$(az webapp config appsettings list --resource-group "$RG" --name "$API_APP" --query "[].name" -o tsv)"
for k in $KEYS_OF_INTEREST; do
  if grep -qx "$k" <<<"$present"; then echo "  present : $k"; else echo "  MISSING : $k"; fi
done

echo
echo "== Portal startup command on $PORTAL_APP (should be: node server.js) =="
az webapp config show --resource-group "$RG" --name "$PORTAL_APP" --query "appCommandLine" -o tsv
