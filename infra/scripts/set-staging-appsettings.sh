#!/usr/bin/env bash
#
# set-staging-appsettings.sh — configure the EXISTING staging App Services to run v2 against
# the EXISTING Azure resources. Run this ONCE (and after any config change) as an authenticated
# `az` user with rights on rg-iip-stg. It sets application settings only — it does NOT create or
# modify any Azure resource, and never touches Partner Center or the Marketplace package.
#
# NO SECRETS ARE STORED IN THIS FILE. Secrets come from Key Vault references (@Microsoft.KeyVault)
# or from your shell environment at run time. Provider auth uses the App Services' managed identity.
#
# Prerequisites (you provide these; the script fails fast if a required one is missing):
#   IIP_PORTAL_ENTRA_CLIENT_ID           client id of the EXISTING app registration
#   IIP_AZURE_OPENAI_CHAT_DEPLOYMENT     e.g. gpt-4o
#   IIP_AZURE_OPENAI_EMBEDDING_DEPLOYMENT e.g. text-embedding-3-large
# Optional (sensible staging defaults shown):
#   RG, API_APP, PORTAL_APP, KV_NAME, SQL_SERVER, SQL_DB, STORAGE, DI_ENDPOINT,
#   SEARCH_ENDPOINT, OPENAI_ENDPOINT, SB_NAMESPACE, APPINSIGHTS_CONNECTION_STRING
set -euo pipefail

RG="${RG:-rg-iip-stg}"
API_APP="${API_APP:-app-api-stg-cus-vyhhymwydvai2}"
PORTAL_APP="${PORTAL_APP:-app-portal-stg-cus-vyhhymwydvai2}"
KV_NAME="${KV_NAME:-kv-stg-vyhhymwydvai2}"
TENANT_ID="${IIP_ENTRA_TENANT_ID:-dcb0b132-fb9e-475c-a68e-b0671222e8d8}"

SQL_SERVER="${SQL_SERVER:-sql-stg-cus-vyhhymwydvai2}"
SQL_DB="${SQL_DB:-insuranceintelligence}"
STORAGE="${STORAGE:-stvyhhymwydvai2}"
DI_ENDPOINT="${DI_ENDPOINT:-https://di-stg-vyhhymwydvai2.cognitiveservices.azure.com/}"
SEARCH_ENDPOINT="${SEARCH_ENDPOINT:-https://srch-stg-vyhhymwydvai2.search.windows.net}"
SEARCH_INDEX="${IIP_AZURE_SEARCH_INDEX_NAME:-insuranceintelligence-knowledge}"
OPENAI_ENDPOINT="${OPENAI_ENDPOINT:-https://oai-stg-vyhhymwydvai2-a5524.openai.azure.com/}"
SB_NAMESPACE="${SB_NAMESPACE:-sb-stg-vyhhymwydvai2.servicebus.windows.net}"

require() { if [ -z "${!1:-}" ]; then echo "ERROR: required env var $1 is not set." >&2; exit 1; fi; }
require IIP_PORTAL_ENTRA_CLIENT_ID
require IIP_AZURE_OPENAI_CHAT_DEPLOYMENT
require IIP_AZURE_OPENAI_EMBEDDING_DEPLOYMENT

CLIENT_ID="$IIP_PORTAL_ENTRA_CLIENT_ID"
KV_URI="https://${KV_NAME}.vault.azure.net/"

# SQL connection: default to Entra Managed Identity (no secret). The API App Service's managed
# identity must be added as a contained DB user with db_datareader/db_datawriter (one-time SQL step).
# To use the SQL admin login instead, store the full connection string as a Key Vault secret named
# 'sql-connection-string' and set: SQL_CONN='@Microsoft.KeyVault(VaultName='"$KV_NAME"';SecretName=sql-connection-string)'
SQL_CONN="${SQL_CONN:-Server=tcp:${SQL_SERVER}.database.windows.net,1433;Database=${SQL_DB};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;}"

echo "Configuring API app settings on $API_APP (resource group $RG)…"
az webapp config appsettings set --resource-group "$RG" --name "$API_APP" --settings \
  INSURANCEINTELLIGENCE_AUTH_MODE=Entra \
  INSURANCEINTELLIGENCE_RUNTIME_PROFILE=Staging \
  INSURANCEINTELLIGENCE_ENTRA_TENANT_ID="$TENANT_ID" \
  INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID="$CLIENT_ID" \
  INSURANCEINTELLIGENCE_ENTRA_AUDIENCE="api://$CLIENT_ID" \
  INSURANCEINTELLIGENCE_STORAGE_PROVIDER=SqlServer \
  INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING="$SQL_CONN" \
  INSURANCEINTELLIGENCE_BLOB_PROVIDER=AzureBlob \
  AzureBlob__AccountName="$STORAGE" \
  AzureBlob__UseManagedIdentity=true \
  INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER=AzureDocumentIntelligence \
  DocumentIntelligence__Endpoint="$DI_ENDPOINT" \
  DocumentIntelligence__UseManagedIdentity=true \
  INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER=AzureAiSearch \
  AzureAiSearch__Endpoint="$SEARCH_ENDPOINT" \
  AzureAiSearch__IndexName="$SEARCH_INDEX" \
  AzureAiSearch__UseManagedIdentity=true \
  INSURANCEINTELLIGENCE_AI_PROVIDER=AzureOpenAI \
  AzureOpenAI__Endpoint="$OPENAI_ENDPOINT" \
  AzureOpenAI__ChatDeployment="$IIP_AZURE_OPENAI_CHAT_DEPLOYMENT" \
  AzureOpenAI__EmbeddingDeployment="$IIP_AZURE_OPENAI_EMBEDDING_DEPLOYMENT" \
  AzureOpenAI__UseManagedIdentity=true \
  INSURANCEINTELLIGENCE_QUEUE_PROVIDER=AzureServiceBus \
  ServiceBus__FullyQualifiedNamespace="$SB_NAMESPACE" \
  ServiceBus__UseManagedIdentity=true \
  INSURANCEINTELLIGENCE_EMAIL_PROVIDER="${IIP_EMAIL_PROVIDER:-Disabled}" \
  INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER=Disabled \
  INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=Disabled \
  Azure__KeyVaultUri="$KV_URI" \
  Onboarding__AdminConsentRedirectUri="https://${PORTAL_APP}.azurewebsites.net/onboarding/consent-callback" \
  1>/dev/null
echo "  API settings applied."

if [ -n "${APPINSIGHTS_CONNECTION_STRING:-}" ]; then
  az webapp config appsettings set --resource-group "$RG" --name "$API_APP" \
    --settings APPLICATIONINSIGHTS_CONNECTION_STRING="$APPINSIGHTS_CONNECTION_STRING" 1>/dev/null
  echo "  Application Insights connection string applied."
fi

echo "Setting Portal startup command (Next.js standalone) on $PORTAL_APP…"
az webapp config set --resource-group "$RG" --name "$PORTAL_APP" --startup-file "node server.js" 1>/dev/null
echo "  Portal startup command set."

cat <<NOTE

Done. Reminders (manual, one-time):
  * Ensure both App Services have a system-assigned managed identity and RBAC on:
      - Key Vault ($KV_NAME): 'Key Vault Secrets User'
      - Storage ($STORAGE): 'Storage Blob Data Contributor'
      - Azure OpenAI ($OPENAI_ENDPOINT): 'Cognitive Services OpenAI User'
      - Document Intelligence: 'Cognitive Services User'
      - AI Search ($SEARCH_ENDPOINT): 'Search Index Data Contributor' + 'Search Service Contributor'
      - Service Bus ($SB_NAMESPACE): 'Azure Service Bus Data Sender'/'Receiver'
  * Add the API App Service managed identity as a contained user in SQL DB '$SQL_DB'
    with db_datareader + db_datawriter (required for the Active Directory Managed Identity
    connection string). Otherwise set SQL_CONN to a Key Vault reference and re-run.
  * Email is set to '${IIP_EMAIL_PROVIDER:-Disabled}'. It stays "Not configured" in the app until
    you set INSURANCEINTELLIGENCE_EMAIL_PROVIDER=Graph AND GraphMail__* credentials AND
    GraphMail__MailboxAccessScoped=true (the API refuses Graph mode otherwise). No fake send path.
NOTE
