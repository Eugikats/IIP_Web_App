# Runs IIP entirely on your machine with NO Azure account, NO credentials, and NO database.
# Storage is in-memory and resets every run; demo data is seeded automatically at startup.
# See docs/02-development-engineering/iip_offline_mode.md
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:INSURANCEINTELLIGENCE_RUNTIME_PROFILE = "LocalTest"
$env:INSURANCEINTELLIGENCE_STORAGE_PROVIDER = "InMemory"
$env:INSURANCEINTELLIGENCE_AUTH_MODE = "Development"
$env:INSURANCEINTELLIGENCE_SEED_DEMO_DATA = "true"
$env:INSURANCEINTELLIGENCE_TENANT_ID = "tenant-demo"
$env:INSURANCEINTELLIGENCE_WORKSPACE_ID = "workspace-operations"
$env:INSURANCEINTELLIGENCE_QUEUE_PROVIDER = "Mock"
$env:INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER = "Disabled"
$env:ServiceBus__FullyQualifiedNamespace = ""

Write-Host "Starting IIP API in offline mode on http://localhost:5167"
dotnet run --project src/InsuranceIntelligence.Api
