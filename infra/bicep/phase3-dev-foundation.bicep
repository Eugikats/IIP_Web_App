targetScope = 'resourceGroup'

@description('Azure region for Phase 3 dev resources. The current roadmap uses westus2 for rg-iip-dev.')
param location string = resourceGroup().location

@description('Environment token used in deterministic resource names.')
param environmentName string = 'dev'

@description('Existing Linux Basic App Service plan created during Phase 3.3.')
param existingAppServicePlanName string = 'asp-iip-dev'

@description('IIP publisher/home Entra tenant id for the multitenant app registration.')
param entraTenantId string = subscription().tenantId

@description('Canonical multitenant IIP API app/client id. Override only for an explicitly approved environment-specific registration.')
param entraApiClientId string = '9ff3410f-12f8-47f9-b249-13bb15c58593'

@description('Canonical multitenant IIP Operations portal SPA app/client id.')
param entraPortalClientId string = '3f44797b-39c8-4d41-aae9-784fbc0aa706'

@description('Delegated API scope requested by the portal SPA.')
param entraApiScope string = 'api://9ff3410f-12f8-47f9-b249-13bb15c58593/access_as_user'

@description('Azure SQL server administrator login. Used for initial server creation only.')
param sqlAdministratorLogin string = 'iipadmin'

@secure()
@description('Azure SQL server administrator password. Supply via CLI or secure pipeline variable; never commit it.')
param sqlAdministratorLoginPassword string

@description('Microsoft Entra login name to configure as the Azure SQL server Entra administrator for dev operations.')
param sqlEntraAdministratorLogin string

@description('Microsoft Entra object id to configure as the Azure SQL server Entra administrator for dev operations.')
param sqlEntraAdministratorObjectId string

@description('Azure SQL database name.')
param sqlDatabaseName string = 'insuranceintelligence'

@description('Azure AI Search index name used by the API when retrieval is enabled.')
param azureSearchIndexName string = 'insuranceintelligence-knowledge'

@description('Optional operations alert email. Empty disables action group and metric alerts.')
param alertEmailOrGroup string = ''

@description('Common resource tags.')
param tags object = {
  product: 'InsuranceIntelligence'
  environment: environmentName
  runtimeProfile: 'Development'
  phase: '3'
  owner: 'ProductEngineering'
}

var uniqueSuffix = uniqueString(resourceGroup().id, environmentName)
var apiAppName = 'app-api-${environmentName}-${uniqueSuffix}'
var portalAppName = 'app-portal-${environmentName}-${uniqueSuffix}'
var functionAppName = 'func-iip-${environmentName}-${uniqueSuffix}'
var storageAccountName = toLower('stiip${uniqueSuffix}')
var keyVaultSuffix = take(uniqueString(resourceGroup().id, environmentName, 'phase3-keyvault'), 8)
var keyVaultName = 'kv-iip-${environmentName}-${keyVaultSuffix}'
var searchServiceName = 'srch-${environmentName}-${uniqueSuffix}'
var serviceBusNamespaceName = 'sb-${environmentName}-${uniqueSuffix}'
var sqlServerName = 'sql-${environmentName}-${uniqueSuffix}'
var logAnalyticsWorkspaceName = 'log-${environmentName}-${uniqueSuffix}'
var appInsightsName = 'appi-${environmentName}-${uniqueSuffix}'
var actionGroupName = 'ag-iip-${environmentName}-${uniqueSuffix}'
var serviceBusDataReceiverRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
var serviceBusDataSenderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
var storageBlobDataOwnerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')

var claimDocumentsContainerName = 'raw-documents'
var knowledgeDocumentsContainerName = 'knowledge-documents'
var evidencePacksContainerName = 'audit-exports'
var knowledgeIndexArtifactsContainerName = 'knowledge-index-artifacts'
var normalizedArtifactsContainerName = 'normalized-artifacts'
var claimPacksContainerName = 'claim-packs'
var blobContainerNames = [
  claimDocumentsContainerName
  knowledgeDocumentsContainerName
  evidencePacksContainerName
  knowledgeIndexArtifactsContainerName
  normalizedArtifactsContainerName
  claimPacksContainerName
]

var serviceBusQueueNames = [
  'email-ingestion'
  'document-processing'
  'knowledge-indexing'
  'outbound-communications'
  'approval-notifications'
  'audit-events'
  'agent-workflow'
]

var storageBlobEndpoint = 'https://${storageAccountName}.blob.${environment().suffixes.storage}'
var serviceBusFullyQualifiedNamespace = '${serviceBusNamespaceName}.servicebus.windows.net'
var sqlConnectionString = 'Server=tcp:${sqlServerName}${environment().suffixes.sqlServerHostname},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var apiBaseUrl = 'https://${apiAppName}.azurewebsites.net'
var portalBaseUrl = 'https://${portalAppName}.azurewebsites.net'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: existingAppServicePlanName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    publicNetworkAccess: 'Enabled'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 30
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 30
    }
  }
}

resource blobContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = [for containerName in blobContainerNames: {
  parent: blobService
  name: containerName
  properties: {
    publicAccess: 'None'
  }
}]

resource searchService 'Microsoft.Search/searchServices@2022-09-01' = {
  name: searchServiceName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'basic'
  }
  properties: {
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http403'
      }
    }
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource serviceBusQueues 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = [for queueName in serviceBusQueueNames: {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
  }
}]

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    version: '12.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource sqlEntraAdministrator 'Microsoft.Sql/servers/administrators@2022-05-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: sqlEntraAdministratorLogin
    sid: sqlEntraAdministratorObjectId
    tenantId: subscription().tenantId
  }
}

resource sqlAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlShortTermRetention 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2022-05-01-preview' = {
  parent: sqlDatabase
  name: 'default'
  properties: {
    retentionDays: 7
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmailOrGroup)) {
  name: actionGroupName
  location: 'Global'
  tags: tags
  properties: {
    groupShortName: 'iipdev'
    enabled: true
    emailReceivers: [
      {
        name: 'operations'
        emailAddress: alertEmailOrGroup
        useCommonAlertSchema: true
      }
    ]
  }
}

var apiAppSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'INSURANCEINTELLIGENCE_RUNTIME_PROFILE'
    value: 'Development'
  }
  {
    name: 'INSURANCEINTELLIGENCE_AUTH_MODE'
    value: 'Entra'
  }
  {
    name: 'INSURANCEINTELLIGENCE_ENTRA_TENANT_ID'
    value: entraTenantId
  }
  {
    name: 'INSURANCEINTELLIGENCE_ENTRA_CLIENT_ID'
    value: entraApiClientId
  }
  {
    name: 'INSURANCEINTELLIGENCE_ENTRA_AUDIENCE'
    value: 'api://${entraApiClientId}'
  }
  {
    name: 'INSURANCEINTELLIGENCE_STORAGE_PROVIDER'
    value: 'SqlServer'
  }
  {
    name: 'INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING'
    value: sqlConnectionString
  }
  {
    name: 'INSURANCEINTELLIGENCE_BLOB_PROVIDER'
    value: 'AzureBlob'
  }
  {
    name: 'INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER'
    value: 'NotConfigured'
  }
  {
    name: 'INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER'
    value: 'AzureAiSearch'
  }
  {
    name: 'INSURANCEINTELLIGENCE_AI_PROVIDER'
    value: 'NotConfigured'
  }
  {
    name: 'INSURANCEINTELLIGENCE_QUEUE_PROVIDER'
    value: 'AzureServiceBus'
  }
  {
    name: 'INSURANCEINTELLIGENCE_EMAIL_PROVIDER'
    value: 'Disabled'
  }
  {
    name: 'INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER'
    value: 'Disabled'
  }
  {
    name: 'INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER'
    value: 'Disabled'
  }
  {
    name: 'INSURANCEINTELLIGENCE_SMS_PROVIDER'
    value: 'Disabled'
  }
  {
    name: 'INSURANCEINTELLIGENCE_CHANNEL_PROVIDER'
    value: 'Disabled'
  }
  {
    name: 'INSURANCEINTELLIGENCE_PROVISIONER_PROVIDER'
    value: 'Disabled'
  }
  {
    name: 'INSURANCEINTELLIGENCE_SEED_DEMO_DATA'
    value: 'false'
  }
  {
    name: 'Azure__KeyVaultUri'
    value: keyVault.properties.vaultUri
  }
  {
    name: 'AzureBlob__AccountName'
    value: storageAccount.name
  }
  {
    name: 'AzureBlob__Endpoint'
    value: storageBlobEndpoint
  }
  {
    name: 'AzureBlob__DocumentContainer'
    value: claimDocumentsContainerName
  }
  {
    name: 'AzureBlob__KnowledgeContainer'
    value: knowledgeDocumentsContainerName
  }
  {
    name: 'AzureBlob__EvidenceContainer'
    value: evidencePacksContainerName
  }
  {
    name: 'AzureBlob__UseManagedIdentity'
    value: 'true'
  }
  {
    name: 'AzureAiSearch__Endpoint'
    value: 'https://${searchServiceName}.search.windows.net'
  }
  {
    name: 'AzureAiSearch__IndexName'
    value: azureSearchIndexName
  }
  {
    name: 'AzureAiSearch__UseManagedIdentity'
    value: 'true'
  }
  {
    name: 'AzureAiSearch__VectorSearchEnabled'
    value: 'false'
  }
  {
    name: 'ServiceBus__FullyQualifiedNamespace'
    value: serviceBusFullyQualifiedNamespace
  }
  {
    name: 'ServiceBus__UseManagedIdentity'
    value: 'true'
  }
  {
    name: 'ServiceBus__EmailIngestionQueue'
    value: 'email-ingestion'
  }
  {
    name: 'ServiceBus__DocumentProcessingQueue'
    value: 'document-processing'
  }
  {
    name: 'ServiceBus__KnowledgeIndexingQueue'
    value: 'knowledge-indexing'
  }
  {
    name: 'ServiceBus__OutboundCommunicationsQueue'
    value: 'outbound-communications'
  }
  {
    name: 'ServiceBus__ApprovalNotificationsQueue'
    value: 'approval-notifications'
  }
  {
    name: 'ServiceBus__AuditEventsQueue'
    value: 'audit-events'
  }
  {
    name: 'ServiceBus__AgentWorkflowQueue'
    value: 'agent-workflow'
  }
  {
    name: 'Portal__CorsOrigin'
    value: portalBaseUrl
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
  {
    name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
    value: '10'
  }
]

var functionAppSettings = concat(apiAppSettings, [
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccount.name
  }
  {
    name: 'ServiceBusConnection__fullyQualifiedNamespace'
    value: serviceBusFullyQualifiedNamespace
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '1'
  }
])

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  kind: 'app,linux'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: false
      http20Enabled: true
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      appSettings: apiAppSettings
    }
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
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

resource apiServiceBusDataSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, apiApp.id, serviceBusDataSenderRoleId)
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: serviceBusDataSenderRoleId
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
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

resource portalApp 'Microsoft.Web/sites@2023-12-01' = {
  name: portalAppName
  location: location
  kind: 'app,linux'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'NODE|20-lts'
      alwaysOn: false
      http20Enabled: true
      ftpsState: 'Disabled'
      appCommandLine: 'node server.js'
      appSettings: [
        {
          name: 'NODE_ENV'
          value: 'production'
        }
        {
          name: 'PORT'
          value: '8080'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'NEXT_PUBLIC_API_BASE_URL'
          value: apiBaseUrl
        }
        {
          name: 'NEXT_PUBLIC_AUTH_MODE'
          value: 'Entra'
        }
        {
          name: 'NEXT_PUBLIC_RUNTIME_PROFILE'
          value: 'Development'
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_TENANT_ID'
          value: entraTenantId
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_CLIENT_ID'
          value: entraPortalClientId
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_PORTAL_CLIENT_ID'
          value: entraPortalClientId
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_API_SCOPE'
          value: entraApiScope
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_REDIRECT_URI'
          value: portalBaseUrl
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

resource apiHigh5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-api-5xx-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: tags
  properties: {
    description: 'API App Service is returning elevated 5xx responses.'
    severity: 2
    enabled: true
    scopes: [
      apiApp.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ApiHttp5xx'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource functionHigh5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-func-5xx-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Functions host is returning elevated 5xx responses.'
    severity: 2
    enabled: true
    scopes: [
      functionApp.id
    ]
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
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource serviceBusDeadLetterAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-sb-dlq-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Service Bus namespace has dead-lettered messages requiring operations review.'
    severity: 2
    enabled: true
    scopes: [
      serviceBusNamespace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ServiceBusDeadLetteredMessages'
          metricNamespace: 'Microsoft.ServiceBus/namespaces'
          metricName: 'DeadletteredMessages'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

output apiAppServiceName string = apiApp.name
output functionAppName string = functionApp.name
output portalAppName string = portalApp.name
output apiBaseUrl string = apiBaseUrl
output portalBaseUrl string = portalBaseUrl
output keyVaultName string = keyVault.name
output storageAccountName string = storageAccount.name
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output aiSearchName string = searchService.name
output serviceBusNamespaceName string = serviceBusNamespace.name
output serviceBusFullyQualifiedNamespace string = serviceBusFullyQualifiedNamespace
output apiPrincipalId string = apiApp.identity.principalId
output functionPrincipalId string = functionApp.identity.principalId
output portalPrincipalId string = portalApp.identity.principalId
output searchPrincipalId string = searchService.identity.principalId
output requiredManualGrants array = [
  'Grant API managed identity Key Vault Secrets User on ${keyVault.name}.'
  'Grant API managed identity Storage Blob Data Contributor on ${storageAccount.name}.'
  'Grant API managed identity Search Index Data Contributor/Search Service Contributor as required on ${searchService.name}.'
  'Create the Functions managed identity as an Azure SQL contained user in ${sqlServer.name}/${sqlDatabase.name} with db_datareader and db_datawriter so delivery status updates persist.'
  'Create the API managed identity as an Azure SQL contained user in ${sqlServer.name}/${sqlDatabase.name} with db_datareader and db_datawriter.'
  'Add ${portalBaseUrl} to the portal Entra app redirect URIs before browser login testing.'
]
