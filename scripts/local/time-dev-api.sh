#!/usr/bin/env bash
# Times the main dev API list calls (five each) and the portal pages. Run it before and after a performance
# change, on the same plan size, and record the results in docs/05-deployment-release-enablement/IIP_B1_PERFORMANCE_PLAN.md.
# About one second of every figure is network distance from a laptop to Azure, not the server.
set -euo pipefail
T=$(az account get-access-token --resource api://d55fe46b-1fba-4c5b-81e1-4ee1918ecf73 --query accessToken -o tsv)
A=https://app-api-dev-h5girbnzrayqe.azurewebsites.net
for path in /health /api/v1/sales-leads /api/v1/customers /api/v1/cases /api/v1/acquisition/channel-intelligence /api/v1/acquisition/escalations /api/v1/brief /api/v1/academy/pricing; do
  ts=""
  for i in 1 2 3 4 5; do ts="$ts $(curl -s -o /dev/null -w '%{time_total}' -H "Authorization: Bearer $T" $A$path)"; done
  echo "$path: $ts" | awk '{printf "%-48s", $1; m=0; s=0; for(i=2;i<=NF;i++){s+=$i; if($i>m)m=$i}; printf "avg %.2fs max %.2fs\n", s/(NF-1), m}'
done
P=https://app-portal-dev-h5girbnzrayqe.azurewebsites.net
for path in /app /app/acquisition /app/academy; do ts=""; for i in 1 2 3; do ts="$ts $(curl -s -o /dev/null -w '%{time_total}' $P$path)"; done; echo "portal $path: $ts"; done
