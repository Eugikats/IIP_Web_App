#!/usr/bin/env bash
# Runs IIP entirely on your machine with NO Azure account, NO credentials, and NO database.
# Storage is in-memory and resets every run; demo data is seeded automatically at startup.
# See docs/02-development-engineering/iip_offline_mode.md
set -euo pipefail

export ASPNETCORE_ENVIRONMENT=Development
export INSURANCEINTELLIGENCE_RUNTIME_PROFILE=LocalTest
export INSURANCEINTELLIGENCE_STORAGE_PROVIDER=InMemory
export INSURANCEINTELLIGENCE_AUTH_MODE=Development
export INSURANCEINTELLIGENCE_SEED_DEMO_DATA=true
export INSURANCEINTELLIGENCE_TENANT_ID=tenant-demo
export INSURANCEINTELLIGENCE_WORKSPACE_ID=workspace-operations

# Every cloud-backed provider stays off. The app degrades honestly to "Not configured"
# rather than pretending these work.
export INSURANCEINTELLIGENCE_QUEUE_PROVIDER=Mock
export INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER=Disabled
export ServiceBus__FullyQualifiedNamespace=""

echo "Starting IIP API in offline mode on http://localhost:5167"
exec dotnet run --project src/InsuranceIntelligence.Api
