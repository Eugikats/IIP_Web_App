targetScope = 'resourceGroup'

param environmentName string = 'dev'
param location string = resourceGroup().location
param entraTenantId string
param entraApiClientId string
param existingAppServicePlanName string = 'asp-iip-${environmentName}'
param sqlDatabaseName string = 'insuranceintelligence'
param azureSearchIndexName string = 'insuranceintelligence-knowledge'
param existingActionGroupName string
param tags object = {
  product: 'InsuranceIntelligence'
  environment: environmentName
  runtimeProfile: 'Development'
  phase: '6'
  owner: 'ProductEngineering'
}

var uniqueSuffix = uniqueString(resourceGroup().id, environmentName)
var functionAppName = 'func-iip-${environmentName}-${uniqueSuffix}'
var portalAppName = 'app-portal-${environmentName}-${uniqueSuffix}'
var storageAccountName = toLower('stiip${uniqueSuffix}')
var keyVaultSuffix = take(uniqueString(resourceGroup().id, environmentName, 'phase3-keyvault'), 8)
var keyVaultName = 'kv-iip-${environmentName}-${keyVaultSuffix}'
var searchServiceName = 'srch-${environmentName}-${uniqueSuffix}'
var serviceBusNamespaceName = 'sb-${environmentName}-${uniqueSuffix}'
var sqlServerName = 'sql-${environmentName}-${uniqueSuffix}'
var appInsightsName = 'appi-${environmentName}-${uniqueSuffix}'
var storageBlobEndpoint = 'https://${storageAccountName}.blob.${environment().suffixes.storage}'
var serviceBusFullyQualifiedNamespace = '${serviceBusNamespaceName}.servicebus.windows.net'
var sqlConnectionString = 'Server=tcp:${sqlServerName}${environment().suffixes.sqlServerHostname},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var portalBaseUrl = 'https://${portalAppName}.azurewebsites.net'
var serviceBusDataReceiverRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
var storageBlobDataOwnerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: existingAppServicePlanName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' existing = {
  name: existingActionGroupName
}

var functionAppSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Development' }
  { name: 'INSURANCEINTELLIGENCE_RUNTIME_PROFILE', value: 'Development' }
  { name: 'INSURANCEINTELLIGENCE_AUTH_MODE', value: 'Entra' }
  { name: 'INSURANCEINTELLIGENCE_ENTRA_TENANT_ID', value: entraTenantId }
  { name: 'INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID', value: entraApiClientId }
  { name: 'INSURANCEINTELLIGENCE_ENTRA_AUDIENCE', value: 'api://${entraApiClientId}' }
  { name: 'INSURANCEINTELLIGENCE_STORAGE_PROVIDER', value: 'SqlServer' }
  { name: 'INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING', value: sqlConnectionString }
  { name: 'INSURANCEINTELLIGENCE_BLOB_PROVIDER', value: 'AzureBlob' }
  { name: 'INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER', value: 'NotConfigured' }
  { name: 'INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER', value: 'AzureAiSearch' }
  { name: 'INSURANCEINTELLIGENCE_AI_PROVIDER', value: 'NotConfigured' }
  { name: 'INSURANCEINTELLIGENCE_QUEUE_PROVIDER', value: 'AzureServiceBus' }
  { name: 'INSURANCEINTELLIGENCE_EMAIL_PROVIDER', value: 'Disabled' }
  { name: 'INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER', value: 'Disabled' }
  { name: 'INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER', value: 'Disabled' }
  { name: 'INSURANCEINTELLIGENCE_SEED_DEMO_DATA', value: 'false' }
  { name: 'Azure__KeyVaultUri', value: keyVault.properties.vaultUri }
  { name: 'AzureBlob__AccountName', value: storageAccount.name }
  { name: 'AzureBlob__Endpoint', value: storageBlobEndpoint }
  { name: 'AzureBlob__DocumentContainer', value: 'raw-documents' }
  { name: 'AzureBlob__KnowledgeContainer', value: 'knowledge-documents' }
  { name: 'AzureBlob__EvidenceContainer', value: 'audit-exports' }
  { name: 'AzureBlob__UseManagedIdentity', value: 'true' }
  { name: 'AzureAiSearch__Endpoint', value: 'https://${searchServiceName}.search.windows.net' }
  { name: 'AzureAiSearch__IndexName', value: azureSearchIndexName }
  { name: 'AzureAiSearch__UseManagedIdentity', value: 'true' }
  { name: 'AzureAiSearch__VectorSearchEnabled', value: 'false' }
  { name: 'ServiceBus__FullyQualifiedNamespace', value: serviceBusFullyQualifiedNamespace }
  { name: 'ServiceBus__UseManagedIdentity', value: 'true' }
  { name: 'ServiceBus__EmailIngestionQueue', value: 'email-ingestion' }
  { name: 'ServiceBus__DocumentProcessingQueue', value: 'document-processing' }
  { name: 'ServiceBus__KnowledgeIndexingQueue', value: 'knowledge-indexing' }
  { name: 'ServiceBus__OutboundCommunicationsQueue', value: 'outbound-communications' }
  { name: 'ServiceBus__ApprovalNotificationsQueue', value: 'approval-notifications' }
  { name: 'ServiceBus__AuditEventsQueue', value: 'audit-events' }
  { name: 'ServiceBus__AgentWorkflowQueue', value: 'agent-workflow' }
  { name: 'Portal__CorsOrigin', value: portalBaseUrl }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
  { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
  { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
  { name: 'AzureWebJobsStorage__accountName', value: storageAccount.name }
  { name: 'ServiceBusConnection__fullyQualifiedNamespace', value: serviceBusFullyQualifiedNamespace }
  { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
  { name: 'ENABLE_ORYX_BUILD', value: 'false' }
]

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      appSettings: functionAppSettings
    }
  }
}

resource functionServiceBusDataReceiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, functionApp.id, serviceBusDataReceiverRoleId)
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: serviceBusDataReceiverRoleId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource functionStorageBlobDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionApp.id, storageBlobDataOwnerRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobDataOwnerRoleId
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource functionHigh5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'ma-func-5xx-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Functions host is returning elevated 5xx responses.'
    severity: 2
    enabled: true
    scopes: [ functionApp.id ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'FunctionHttp5xx'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [ { actionGroupId: actionGroup.id } ]
  }
}

output functionAppName string = functionApp.name
output functionPrincipalId string = functionApp.identity.principalId
output functionDefaultHostName string = functionApp.properties.defaultHostName
output functionHigh5xxAlertName string = functionHigh5xxAlert.name
