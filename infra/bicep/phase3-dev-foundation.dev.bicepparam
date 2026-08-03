using './phase3-dev-foundation.bicep'

param location = 'westus2'
param environmentName = 'dev'
param existingAppServicePlanName = 'asp-iip-dev'

param entraTenantId = readEnvironmentVariable('IIP_DEV_ENTRA_TENANT_ID', 'dcb0b132-fb9e-475c-a68e-b0671222e8d8')
param entraApiClientId = readEnvironmentVariable('IIP_DEV_API_CLIENT_ID', '9ff3410f-12f8-47f9-b249-13bb15c58593')
param entraPortalClientId = readEnvironmentVariable('IIP_DEV_PORTAL_CLIENT_ID', '3f44797b-39c8-4d41-aae9-784fbc0aa706')
param entraApiScope = readEnvironmentVariable('IIP_DEV_API_SCOPE', 'api://9ff3410f-12f8-47f9-b249-13bb15c58593/access_as_user')

param sqlAdministratorLogin = readEnvironmentVariable('IIP_DEV_SQL_ADMIN_LOGIN', 'iipadmin')
param sqlAdministratorLoginPassword = readEnvironmentVariable('IIP_DEV_SQL_ADMIN_PASSWORD')
param sqlEntraAdministratorLogin = readEnvironmentVariable('IIP_DEV_SQL_ENTRA_ADMIN_LOGIN', 'harrygafa0_gmail.com#EXT#@oticgroupug.onmicrosoft.com')
param sqlEntraAdministratorObjectId = readEnvironmentVariable('IIP_DEV_SQL_ENTRA_ADMIN_OBJECT_ID', '5b303954-53fe-45de-b094-c8f70e4aef1c')
param alertEmailOrGroup = readEnvironmentVariable('IIP_DEV_ALERT_EMAIL_OR_GROUP', '')
