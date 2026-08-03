targetScope = 'resourceGroup'

@description('Deployment region.')
param location string = resourceGroup().location

@description('Environment name, for example dev, pilot, or production.')
param environmentName string

@allowed([
  'auto'
  'Development'
  'Staging'
  'Production'
  'ManagedApplicationReviewer'
])
@description('IIP runtime profile enforced by API readiness and startup gates. Auto maps dev to Development, production size/name to Production, and other non-dev deployments to Staging.')
param runtimeProfile string = 'auto'

@description('Insurer or tenant display name.')
param insurerDisplayName string = 'InsuranceIntelligence Dev'

@allowed([
  'dev'
  'pilot'
  'production'
])
@description('Azure deployment size profile used by Phase 8A resources.')
param deploymentSize string = 'dev'

@allowed([
  'dev'
  'pilot'
  'production'
])
@description('Legacy SKU size profile alias kept for managed-app packaging compatibility.')
param skuSizeProfile string = 'dev'

@description('Business lines enabled for this deployment.')
param businessLinesEnabled array = [
  'motor'
]

@allowed([
  'none'
  'read_only'
  'delegated'
  'jit'
])
@description('Publisher support access mode.')
param supportAccessMode string = 'none'

@description('Private networking enabled. Legacy alias kept for managed-app packaging compatibility.')
param privateNetworkingEnabled bool = false

@description('Enable private endpoints for regulated environments. Phase 8A dev defaults to false.')
param enablePrivateEndpoints bool = false

@description('Enable managed identities on app hosts and provider-capable resources.')
param useManagedIdentity bool = true

@allowed([
  'mock'
  'azure_openai'
  'foundry_agent_service'
])
@description('AI provider mode exposed to managed-app packaging. Mock remains the default.')
param aiProviderMode string = 'mock'

@allowed([
  'regional'
  'data_zone'
  'global'
])
@description('Data residency mode.')
param dataResidencyMode string = 'regional'

@description('Approved country or jurisdiction for customer-data residency. Required for Production and ManagedApplicationReviewer.')
param dataResidencyCountry string = ''

@description('Approved Azure geography or data boundary, for example Africa or EU. Required for Production and ManagedApplicationReviewer.')
param dataResidencyGeography string = ''

@description('Comma-separated Azure region names approved by legal, security, and the customer. Required for Production and ManagedApplicationReviewer.')
param dataResidencyAllowedAzureRegions string = ''

@description('Non-secret legal/security approval or decision-record reference. Required for Production and ManagedApplicationReviewer.')
param dataResidencyApprovalReference string = ''

@allowed([
  'Standard_LRS'
  'Standard_ZRS'
  'Standard_GRS'
  'Standard_GZRS'
])
@description('Storage replication SKU. LRS is the residency-safe default. GRS/GZRS require the paired secondary region to be approved and listed in the residency decision.')
param storageAccountSkuName string = 'Standard_LRS'

@minValue(30)
@description('Document retention in days.')
param documentRetentionDays int = 365

@allowed([
  'local_mock'
  'storage_account_placeholder'
  'azure_blob'
])
@description('Document storage mode. Local mock remains default; azure_blob switches app settings to Azure Blob.')
param documentStorageMode string = 'local_mock'

@allowed([
  'mock'
  'azure_document_intelligence_placeholder'
  'azure_document_intelligence'
  'existing_resource_placeholder'
])
@description('Document Intelligence mode. Mock remains default; azure_document_intelligence switches app settings.')
param documentIntelligenceMode string = 'mock'

@description('Create a dev Document Intelligence account. The app still uses mock mode unless documentIntelligenceMode is azure_document_intelligence.')
param deployDocumentIntelligence bool = true

@description('Optional existing Document Intelligence endpoint placeholder. Leave empty for local mock mode.')
param existingDocumentIntelligenceEndpoint string = ''

@allowed([
  'mock'
  'azure_ai_search_placeholder'
  'azure_ai_search'
  'existing_resource_placeholder'
])
@description('Knowledge retrieval/search mode. Mock remains default; azure_ai_search switches app settings.')
param knowledgeSearchMode string = 'mock'

@description('Create a dev Azure AI Search service. The app still uses mock mode unless knowledgeSearchMode is azure_ai_search.')
param deployAiSearch bool = true

@description('Azure AI Search index name used when retrieval mode is AzureAiSearch.')
param azureSearchIndexName string = 'insuranceintelligence-knowledge'

@allowed([
  'mock'
  'azure_openai_placeholder'
  'azure_openai'
  'foundry_agent_placeholder'
  'existing_resource_placeholder'
])
@description('Assistant AI provider mode. Mock remains default; azure_openai switches app settings.')
param assistantAiProviderMode string = 'mock'

@description('Create a dev Azure OpenAI account. Default is false because availability and quota are subscription/region dependent.')
param deployAzureOpenAi bool = false
@allowed([
  ''
  'Standard'
  'DataZoneStandard'
  'GlobalStandard'
])
@description('Deployment type of the externally created Azure OpenAI model deployments. Protected production runtimes require this to match dataResidencyMode.')
param azureOpenAiDeploymentType string = ''


@description('Azure OpenAI chat deployment name to use after a deployment is created outside this dev template.')
param azureOpenAiChatDeployment string = ''

@description('Azure OpenAI embedding deployment name to use after a deployment is created outside this dev template.')
param azureOpenAiEmbeddingDeployment string = ''

@description('Blob container name for raw knowledge documents.')
param knowledgeDocumentsContainerName string = 'knowledge-documents'

@description('Blob container name for raw claim documents.')
param claimDocumentsContainerName string = 'raw-documents'

@description('Blob container name for audit/evidence exports.')
param evidencePacksContainerName string = 'audit-exports'

@description('Blob container name for knowledge index artifacts.')
param knowledgeIndexArtifactsContainerName string = 'knowledge-index-artifacts'

@description('Blob container name for normalized extraction artifacts.')
param normalizedArtifactsContainerName string = 'normalized-artifacts'

@description('Blob container name for generated claim packs.')
param claimPacksContainerName string = 'claim-packs'

@allowed([
  'mock'
  'azure_communication_services_placeholder'
  'existing_resource_placeholder'
  'disabled'
])
@description('Communication Services provider mode placeholder. Phase 8A does not send live customer messages by default.')
param communicationServicesMode string = 'mock'

@allowed([
  'mock'
  'microsoft_graph_mail'
  'microsoft_graph_mail_placeholder'
  'existing_resource_placeholder'
])
@description('Microsoft Graph mail mode placeholder. Phase 8A does not send live Outlook mail by default.')
param graphMailMode string = 'mock'

@description('Set true only after Microsoft Graph app mailbox access is restricted to approved insurer mailboxes through Exchange Online RBAC for Applications or an Application Access Policy.')
param graphMailboxAccessScoped bool = false

@description('Future Microsoft Graph application client ID setting placeholder.')
param graphClientIdSettingName string = 'INSURANCEINTELLIGENCE_GRAPH_CLIENT_ID'

@description('Future Microsoft Graph shared mailbox address setting placeholder.')
param graphSharedMailboxSettingName string = 'INSURANCEINTELLIGENCE_GRAPH_MAILBOX_ADDRESS'

@allowed([
  'mock'
  'teams_notification_placeholder'
  'existing_resource_placeholder'
  'disabled'
])
@description('Teams notification provider mode placeholder. Phase 8A does not send live Teams notifications by default.')
param teamsNotificationMode string = 'mock'

@description('Future Communication Services connection setting placeholder. Value must be stored outside source.')
param communicationServicesConnectionSettingName string = 'INSURANCEINTELLIGENCE_ACS_CONNECTION'

@allowed([
  'InMemory'
  'SqlServer'
  'in_memory'
  'azure_sql_placeholder'
])
@description('Transactional storage provider mode. Azure dev defaults to InMemory for smoke tests with no secrets.')
param storageProviderMode string = 'InMemory'

@description('Create Azure SQL dev placeholders. Default is false to avoid requiring a SQL admin secret.')
param deployAzureSql bool = false

@description('Azure SQL server administrator login. Used only when deployAzureSql is true.')
param sqlAdministratorLogin string = 'iiadmin'

@secure()
@description('Azure SQL server administrator password. Provide only through CLI, environment, or secure pipeline variables when deployAzureSql is true.')
param sqlAdministratorLoginPassword string = ''

@description('Azure SQL database name. Created only when deployAzureSql is true.')
param sqlDatabaseName string = 'insuranceintelligence'

@description('Future Azure SQL connection setting name placeholder. Value must come from Key Vault/App Service settings, not this template.')
param databaseConnectionSettingName string = 'INSURANCEINTELLIGENCE_SQLSERVER_CONNECTION_STRING'

@description('Managed identity database access placeholder. Phase 8A does not grant live SQL database roles.')
param managedIdentityDatabaseAccessEnabled bool = false

@description('Create a dev Service Bus namespace and queues. The app still uses mock queues unless workflowApprovalQueueMode is service_bus.')
param deployServiceBus bool = true

@description('Service Bus queues prepared for async workers.')
param serviceBusQueueNames array = [
  'email-ingestion'
  'document-processing'
  'outbound-communications'
  'approval-notifications'
  'agent-workflow'
]

@allowed([
  'development'
  'entra'
  'entra_placeholder'
])
@description('Authentication mode placeholder. Azure dev starts with development demo headers.')
param authMode string = 'development'

@description('Microsoft Entra tenant ID used by the portal browser sign-in flow.')
param portalEntraTenantId string = subscription().tenantId

@description('Public client application ID for the portal SPA sign-in flow.')
param portalEntraClientId string = ''

@description('Delegated API scope requested by the portal SPA, for example api://<api-app-id>/access_as_user.')
param portalEntraApiScope string = ''

@description('Redirect URI used by the portal browser sign-in flow.')
param portalEntraRedirectUri string = ''

@allowed([
  'in_memory'
  'service_bus_placeholder'
  'service_bus'
])
@description('Workflow and approval queue mode. In-memory remains default; service_bus switches app settings to Azure Service Bus.')
param workflowApprovalQueueMode string = 'in_memory'

@minValue(90)
@description('Audit retention in days.')
param auditRetentionDays int = 2555

@description('Operational alert email or group.')
param alertEmailOrGroup string = ''

@description('Outlook/shared mailbox integration enabled.')
param outlookSharedMailboxEnabled bool = false

@description('SMS/email module enabled.')
param smsEmailModuleEnabled bool = false

@description('Teams notifications enabled.')
param teamsNotificationsEnabled bool = false

@description('SharePoint sync enabled.')
param sharePointSyncEnabled bool = false

@description('Voice module enabled.')
param voiceModuleEnabled bool = false

@description('WhatsApp module enabled.')
param whatsAppModuleEnabled bool = false

var uniqueSuffix = uniqueString(resourceGroup().id, environmentName)
var normalizedEnvironmentName = toLower(environmentName)
var inferredRuntimeProfile = normalizedEnvironmentName == 'dev' || normalizedEnvironmentName == 'development' ? 'Development' : normalizedEnvironmentName == 'production' || deploymentSize == 'production' ? 'Production' : 'Staging'
var appRuntimeProfile = runtimeProfile == 'auto' ? inferredRuntimeProfile : runtimeProfile
var productionResidencyApprovalRequired = appRuntimeProfile == 'Production' || appRuntimeProfile == 'ManagedApplicationReviewer'
var normalizedAllowedAzureRegions = split(toLower(replace(dataResidencyAllowedAzureRegions, ' ', '')), ',')
var productionResidencyConfigured = !productionResidencyApprovalRequired || (!empty(dataResidencyCountry) && !empty(dataResidencyGeography) && !empty(dataResidencyAllowedAzureRegions) && !empty(dataResidencyApprovalReference))
var productionRegionApproved = !productionResidencyApprovalRequired || contains(normalizedAllowedAzureRegions, toLower(location))
var privateAccessEnabled = enablePrivateEndpoints || privateNetworkingEnabled
var resourceTags = {
  product: 'InsuranceIntelligence'
  environment: environmentName
  runtimeProfile: appRuntimeProfile
  insurer: insurerDisplayName
  phase: '8A'
  deploymentSize: deploymentSize
  skuSizeProfile: skuSizeProfile
  aiProviderMode: aiProviderMode
  supportAccessMode: supportAccessMode
  dataResidencyMode: dataResidencyMode
  privateNetworkingEnabled: string(privateAccessEnabled)
  documentStorageMode: documentStorageMode
  documentIntelligenceMode: documentIntelligenceMode
  knowledgeSearchMode: knowledgeSearchMode
  assistantAiProviderMode: assistantAiProviderMode
  communicationServicesMode: communicationServicesMode
  graphMailMode: graphMailMode
  graphMailboxAccessScoped: string(graphMailboxAccessScoped)
  graphClientIdSettingName: graphClientIdSettingName
  graphSharedMailboxSettingName: graphSharedMailboxSettingName
  teamsNotificationMode: teamsNotificationMode
  communicationServicesConnectionSettingName: communicationServicesConnectionSettingName
  workflowApprovalQueueMode: workflowApprovalQueueMode
  storageProviderMode: storageProviderMode
  authMode: authMode
  managedIdentityDatabaseAccessEnabled: string(managedIdentityDatabaseAccessEnabled)
}

var appServicePlanName = 'asp-${environmentName}-${uniqueSuffix}'
var apiAppServiceName = 'app-api-${environmentName}-${uniqueSuffix}'
var portalAppName = 'app-portal-${environmentName}-${uniqueSuffix}'
var functionAppName = 'func-${environmentName}-${uniqueSuffix}'
var storageAccountName = toLower('st${uniqueSuffix}')
var serviceBusNamespaceName = 'sb-${environmentName}-${uniqueSuffix}'
var keyVaultName = 'kv-${environmentName}-${uniqueSuffix}'
var searchServiceName = 'srch-${environmentName}-${uniqueSuffix}'
var documentIntelligenceAccountName = 'di-${environmentName}-${uniqueSuffix}'
var openAiAccountName = 'oai-${environmentName}-${uniqueSuffix}'
var logAnalyticsWorkspaceName = 'log-${environmentName}-${uniqueSuffix}'
var appInsightsName = 'appi-${environmentName}-${uniqueSuffix}'
var sqlServerName = 'sql-${environmentName}-${uniqueSuffix}'
var operationalActionGroupName = 'ag-iip-${environmentName}-${uniqueSuffix}'
var operationalActionGroupShortName = 'iip${take(environmentName, 8)}'

var appServicePlanSku = deploymentSize == 'production' ? {
  name: 'P1v3'
  tier: 'PremiumV3'
  capacity: 2
} : deploymentSize == 'pilot' ? {
  name: 'S1'
  tier: 'Standard'
  capacity: 1
} : {
  name: 'B1'
  tier: 'Basic'
  capacity: 1
}

var storageSkuName = storageAccountSkuName
var searchSkuName = deploymentSize == 'production' ? 'standard' : 'basic'
var serviceBusSku = deploymentSize == 'production' ? 'Standard' : 'Basic'
var sqlSkuName = deploymentSize == 'production' ? 'GP_Gen5_2' : 'Basic'
var sqlSkuTier = deploymentSize == 'production' ? 'GeneralPurpose' : 'Basic'

var provisionDocumentIntelligence = deployDocumentIntelligence || documentIntelligenceMode == 'azure_document_intelligence' || documentIntelligenceMode == 'azure_document_intelligence_placeholder'
var provisionSearch = deployAiSearch || knowledgeSearchMode == 'azure_ai_search' || knowledgeSearchMode == 'azure_ai_search_placeholder'
var provisionOpenAi = deployAzureOpenAi || assistantAiProviderMode == 'azure_openai' || assistantAiProviderMode == 'azure_openai_placeholder' || aiProviderMode == 'azure_openai'
var provisionServiceBus = deployServiceBus || workflowApprovalQueueMode == 'service_bus' || workflowApprovalQueueMode == 'service_bus_placeholder'
var provisionSql = deployAzureSql || storageProviderMode == 'SqlServer' || storageProviderMode == 'azure_sql_placeholder'

var appStorageProvider = storageProviderMode == 'in_memory' ? 'InMemory' : storageProviderMode == 'azure_sql_placeholder' ? 'SqlServer' : storageProviderMode
var appAuthMode = authMode == 'development' ? 'Development' : 'Entra'
var appBlobProvider = documentStorageMode == 'azure_blob' ? 'AzureBlob' : 'Mock'
var appDocumentProvider = documentIntelligenceMode == 'azure_document_intelligence' ? 'AzureDocumentIntelligence' : 'Mock'
var appRetrievalProvider = knowledgeSearchMode == 'azure_ai_search' ? 'AzureAiSearch' : 'Mock'
var appAiProvider = assistantAiProviderMode == 'azure_openai' || aiProviderMode == 'azure_openai' ? 'AzureOpenAI' : 'Mock'
var appQueueProvider = workflowApprovalQueueMode == 'service_bus' ? 'AzureServiceBus' : 'Mock'
var appEmailProvider = graphMailMode == 'microsoft_graph_mail' || graphMailMode == 'microsoft_graph_mail_placeholder' || graphMailMode == 'existing_resource_placeholder' ? 'Graph' : 'Mock'
var appCommunicationProvider = graphMailMode == 'microsoft_graph_mail' || graphMailMode == 'microsoft_graph_mail_placeholder' || graphMailMode == 'existing_resource_placeholder' ? 'GraphEmail' : 'Mock'
var appNotificationProvider = teamsNotificationMode == 'teams_notification_placeholder' ? 'Teams' : 'Disabled'
var serviceBusQueues = union(serviceBusQueueNames, [
  'knowledge-indexing'
  'audit-events'
])
var blobContainerNames = [
  claimDocumentsContainerName
  knowledgeDocumentsContainerName
  knowledgeIndexArtifactsContainerName
  evidencePacksContainerName
  normalizedArtifactsContainerName
  claimPacksContainerName
]
var storageBlobEndpoint = 'https://${storageAccountName}.blob.${environment().suffixes.storage}'
var serviceBusNamespaceEndpoint = '${serviceBusNamespaceName}.servicebus.windows.net'
var documentIntelligenceEndpoint = provisionDocumentIntelligence ? documentIntelligenceAccount!.properties.endpoint : existingDocumentIntelligenceEndpoint
var searchEndpoint = provisionSearch ? 'https://${searchServiceName}.search.windows.net' : ''
var openAiEndpoint = provisionOpenAi ? 'https://${openAiAccountName}.openai.azure.com/' : ''
var managedIdentityFlag = string(useManagedIdentity)

var apiAppSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'INSURANCEINTELLIGENCE_RUNTIME_PROFILE'
    value: appRuntimeProfile
  }
  {
    name: 'INSURANCEINTELLIGENCE_AZURE_REGION'
    value: location
  }
  {
    name: 'INSURANCEINTELLIGENCE_DATA_RESIDENCY_COUNTRY'
    value: dataResidencyCountry
  }
  {
    name: 'INSURANCEINTELLIGENCE_DATA_RESIDENCY_GEOGRAPHY'
    value: dataResidencyGeography
  }
  {
    name: 'INSURANCEINTELLIGENCE_DATA_RESIDENCY_MODE'
    value: dataResidencyMode
  }
  {
    name: 'INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS'
    value: dataResidencyAllowedAzureRegions
  }
  {
    name: 'INSURANCEINTELLIGENCE_DATA_RESIDENCY_APPROVAL_REFERENCE'
    value: dataResidencyApprovalReference
  }
  {
    name: 'INSURANCEINTELLIGENCE_STORAGE_PROVIDER'
    value: appStorageProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_BLOB_PROVIDER'
    value: appBlobProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER'
    value: appDocumentProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER'
    value: appRetrievalProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_AI_PROVIDER'
    value: appAiProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_QUEUE_PROVIDER'
    value: appQueueProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_EMAIL_PROVIDER'
    value: appEmailProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_NOTIFICATION_PROVIDER'
    value: appNotificationProvider
  }
  {
    name: 'INSURANCEINTELLIGENCE_COMMUNICATION_PROVIDER'
    value: appCommunicationProvider
  }
  {
    name: 'GraphMail__MailboxAccessScoped'
    value: string(graphMailboxAccessScoped)
  }
  {
    name: 'INSURANCEINTELLIGENCE_AUTH_MODE'
    value: appAuthMode
  }
  {
    name: 'INSURANCEINTELLIGENCE_SEED_DEMO_DATA'
    value: environmentName == 'dev' ? 'true' : 'false'
  }
  {
    name: databaseConnectionSettingName
    value: ''
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
    value: managedIdentityFlag
  }
  {
    name: 'DocumentIntelligence__Endpoint'
    value: documentIntelligenceEndpoint
  }
  {
    name: 'DocumentIntelligence__ModelMode'
    value: 'Prebuilt'
  }
  {
    name: 'DocumentIntelligence__DefaultModelId'
    value: 'prebuilt-document'
  }
  {
    name: 'DocumentIntelligence__UseManagedIdentity'
    value: managedIdentityFlag
  }
  {
    name: 'AzureAiSearch__Endpoint'
    value: searchEndpoint
  }
  {
    name: 'AzureAiSearch__IndexName'
    value: azureSearchIndexName
  }
  {
    name: 'AzureAiSearch__UseManagedIdentity'
    value: managedIdentityFlag
  }
  {
    name: 'AzureAiSearch__VectorSearchEnabled'
    value: 'false'
  }
  {
    name: 'AzureOpenAI__Endpoint'
    value: openAiEndpoint
  }
  {
    name: 'AzureOpenAI__DeploymentType'
    value: azureOpenAiDeploymentType
  }
  {
    name: 'AzureOpenAI__ChatDeployment'
    value: azureOpenAiChatDeployment
  }
  {
    name: 'AzureOpenAI__EmbeddingDeployment'
    value: azureOpenAiEmbeddingDeployment
  }
  {
    name: 'AzureOpenAI__UseManagedIdentity'
    value: managedIdentityFlag
  }
  {
    name: 'AzureOpenAI__MaxTokens'
    value: '800'
  }
  {
    name: 'AzureOpenAI__Temperature'
    value: '0.1'
  }
  {
    name: 'AzureOpenAI__RequestTimeoutSeconds'
    value: '30'
  }
  {
    name: 'ServiceBus__FullyQualifiedNamespace'
    value: provisionServiceBus ? serviceBusNamespaceEndpoint : ''
  }
  {
    name: 'ServiceBus__UseManagedIdentity'
    value: managedIdentityFlag
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
    name: 'ProviderHealth__TimeoutSeconds'
    value: '5'
  }
  {
    name: 'Portal__CorsOrigin'
    value: 'https://${portalAppName}.azurewebsites.net'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsights.properties.ConnectionString
  }
  {
    name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
    value: appInsights.properties.InstrumentationKey
  }
  {
    name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
    value: '10'
  }
]

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: resourceTags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: deploymentSize == 'production' ? 90 : 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: resourceTags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource operationalActionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmailOrGroup)) {
  name: operationalActionGroupName
  location: 'Global'
  tags: resourceTags
  properties: {
    groupShortName: operationalActionGroupShortName
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

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  tags: resourceTags
  sku: appServicePlanSku
  properties: {
    reserved: true
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: resourceTags
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
    publicNetworkAccess: privateAccessEnabled ? 'Disabled' : 'Enabled'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: resourceTags
  sku: {
    name: storageSkuName
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: privateAccessEnabled ? 'Disabled' : 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: min(documentRetentionDays, 365)
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: min(documentRetentionDays, 365)
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

resource storageManagementPolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          enabled: deploymentSize == 'production'
          name: 'coolDocumentAndEvidenceBlobs'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 30
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                '${claimDocumentsContainerName}/'
                '${knowledgeDocumentsContainerName}/'
                '${evidencePacksContainerName}/'
                '${normalizedArtifactsContainerName}/'
                '${claimPacksContainerName}/'
              ]
            }
          }
        }
      ]
    }
  }
}

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = if (provisionSql) {
  name: sqlServerName
  location: location
  tags: resourceTags
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: privateAccessEnabled ? 'Disabled' : 'Enabled'
    version: '12.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = if (provisionSql) {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: resourceTags
  sku: {
    name: sqlSkuName
    tier: sqlSkuTier
  }
}

resource sqlShortTermRetention 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2022-05-01-preview' = if (provisionSql) {
  parent: sqlDatabase
  name: 'default'
  properties: {
    retentionDays: deploymentSize == 'production' ? 35 : 7
  }
}

resource documentIntelligenceAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (provisionDocumentIntelligence) {
  name: documentIntelligenceAccountName
  location: location
  tags: resourceTags
  kind: 'FormRecognizer'
  identity: {
    type: useManagedIdentity ? 'SystemAssigned' : 'None'
  }
  sku: {
    name: deploymentSize == 'production' ? 'S0' : 'F0'
  }
  properties: {
    publicNetworkAccess: privateAccessEnabled ? 'Disabled' : 'Enabled'
  }
}

resource searchService 'Microsoft.Search/searchServices@2022-09-01' = if (provisionSearch) {
  name: searchServiceName
  location: location
  tags: resourceTags
  identity: {
    type: useManagedIdentity ? 'SystemAssigned' : 'None'
  }
  sku: {
    name: searchSkuName
  }
  properties: {
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http403'
      }
    }
    hostingMode: 'default'
    publicNetworkAccess: privateAccessEnabled ? 'disabled' : 'enabled'
  }
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = if (provisionOpenAi) {
  name: openAiAccountName
  location: location
  tags: resourceTags
  kind: 'OpenAI'
  identity: {
    type: useManagedIdentity ? 'SystemAssigned' : 'None'
  }
  sku: {
    name: 'S0'
  }
  properties: {
    publicNetworkAccess: privateAccessEnabled ? 'Disabled' : 'Enabled'
  }
}

module serviceBusModule 'modules/messaging/servicebus.bicep' = if (provisionServiceBus) {
  name: 'serviceBusDeployment'
  params: {
    namespaceName: serviceBusNamespaceName
    location: location
    skuName: serviceBusSku
    queueNames: serviceBusQueues
    disableLocalAuth: true
    publicNetworkAccessDisabled: privateAccessEnabled
    tags: resourceTags
  }
}

resource apiHigh5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-api-5xx-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: resourceTags
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
        actionGroupId: operationalActionGroup.id
      }
    ]
  }
}

resource apiHighLatencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-api-latency-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: resourceTags
  properties: {
    description: 'API App Service average response time is above the production operations threshold.'
    severity: 3
    enabled: true
    scopes: [
      apiApp.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ApiAverageResponseTime'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'AverageResponseTime'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: operationalActionGroup.id
      }
    ]
  }
}

resource functionHigh5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-func-5xx-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: resourceTags
  properties: {
    description: 'Functions host is returning elevated 5xx responses.'
    severity: 2
    enabled: true
    scopes: [
      functionAppModule.outputs.functionAppId
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
        actionGroupId: operationalActionGroup.id
      }
    ]
  }
}

resource portalHigh5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmailOrGroup)) {
  name: 'ma-portal-5xx-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: resourceTags
  properties: {
    description: 'Portal App Service is returning elevated 5xx responses.'
    severity: 3
    enabled: true
    scopes: [
      portalApp.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'PortalHttp5xx'
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
        actionGroupId: operationalActionGroup.id
      }
    ]
  }
}

resource serviceBusDeadLetterAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (provisionServiceBus && !empty(alertEmailOrGroup)) {
  name: 'ma-sb-dlq-${environmentName}-${uniqueSuffix}'
  location: 'global'
  tags: resourceTags
  properties: {
    description: 'Service Bus namespace has dead-lettered messages requiring operations review.'
    severity: 2
    enabled: true
    scopes: [
      serviceBusModule.outputs.namespaceId
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
        actionGroupId: operationalActionGroup.id
      }
    ]
  }
}

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppServiceName
  location: location
  kind: 'app,linux'
  tags: resourceTags
  identity: {
    type: useManagedIdentity ? 'SystemAssigned' : 'None'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: deploymentSize != 'dev'
      http20Enabled: true
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      appSettings: apiAppSettings
    }
  }
}

resource portalApp 'Microsoft.Web/sites@2023-12-01' = {
  name: portalAppName
  location: location
  kind: 'app,linux'
  tags: resourceTags
  identity: {
    type: useManagedIdentity ? 'SystemAssigned' : 'None'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'NODE|20-lts'
      alwaysOn: deploymentSize != 'dev'
      http20Enabled: true
      ftpsState: 'Disabled'
      appCommandLine: 'bash -c "if [ ! -d node_modules/next ] && [ -f node_modules.tar.gz ]; then mkdir -p node_modules && tar --touch --no-same-owner --no-same-permissions -xzf node_modules.tar.gz -C node_modules; fi; node server.js"'
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
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '1800'
        }
        {
          name: 'NEXT_PUBLIC_API_BASE_URL'
          value: 'https://${apiAppServiceName}.azurewebsites.net'
        }
        {
          name: 'NEXT_PUBLIC_TENANT_ID'
          value: 'tenant-demo'
        }
        {
          name: 'NEXT_PUBLIC_WORKSPACE_ID'
          value: 'workspace-claims'
        }
        {
          name: 'NEXT_PUBLIC_AUTH_MODE'
          value: appAuthMode
        }
        {
          name: 'NEXT_PUBLIC_RUNTIME_PROFILE'
          value: appRuntimeProfile
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_TENANT_ID'
          value: portalEntraTenantId
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_CLIENT_ID'
          value: portalEntraClientId
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_PORTAL_CLIENT_ID'
          value: portalEntraClientId
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_API_SCOPE'
          value: portalEntraApiScope
        }
        {
          name: 'NEXT_PUBLIC_ENTRA_REDIRECT_URI'
          value: portalEntraRedirectUri
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

module functionAppModule 'modules/compute/functionapp.bicep' = {
  name: 'functionAppDeployment'
  params: {
    functionAppName: functionAppName
    location: location
    appServicePlanId: appServicePlan.id
    storageAccountName: 'stfa${uniqueSuffix}'
    useManagedIdentity: useManagedIdentity
    publicNetworkAccessDisabled: privateAccessEnabled
    appSettings: apiAppSettings
    tags: resourceTags
  }
}

output phase string = 'Phase 8A Azure dev environment deployment path with mock-first provider modes.'
output applicationInsightsName string = appInsights.name
output logAnalyticsWorkspaceName string = logAnalytics.name
output appServicePlanName string = appServicePlan.name
output apiAppServiceName string = apiApp.name
output portalAppName string = portalApp.name
output functionAppName string = functionAppName
output apiBaseUrl string = 'https://${apiApp.name}.azurewebsites.net'
output portalBaseUrl string = 'https://${portalApp.name}.azurewebsites.net'
output keyVaultResourceName string = keyVault.name
output storageAccountName string = storageAccount.name
output sqlServerName string = provisionSql ? sqlServerName : ''
output sqlDatabaseName string = provisionSql ? sqlDatabaseName : ''
output aiSearchName string = provisionSearch ? searchServiceName : ''
output documentIntelligenceName string = provisionDocumentIntelligence ? documentIntelligenceAccountName : ''
output azureOpenAiName string = provisionOpenAi ? openAiAccountName : ''
output serviceBusNamespaceName string = provisionServiceBus ? serviceBusNamespaceName : ''
output appSettings object = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  INSURANCEINTELLIGENCE_RUNTIME_PROFILE: appRuntimeProfile
  INSURANCEINTELLIGENCE_AZURE_REGION: location
  INSURANCEINTELLIGENCE_DATA_RESIDENCY_COUNTRY: dataResidencyCountry
  INSURANCEINTELLIGENCE_DATA_RESIDENCY_GEOGRAPHY: dataResidencyGeography
  INSURANCEINTELLIGENCE_DATA_RESIDENCY_MODE: dataResidencyMode
  INSURANCEINTELLIGENCE_DATA_RESIDENCY_ALLOWED_AZURE_REGIONS: dataResidencyAllowedAzureRegions
  INSURANCEINTELLIGENCE_DATA_RESIDENCY_APPROVAL_REFERENCE: dataResidencyApprovalReference
  INSURANCEINTELLIGENCE_STORAGE_PROVIDER: appStorageProvider
  INSURANCEINTELLIGENCE_BLOB_PROVIDER: appBlobProvider
  INSURANCEINTELLIGENCE_DOCUMENT_PROVIDER: appDocumentProvider
  INSURANCEINTELLIGENCE_RETRIEVAL_PROVIDER: appRetrievalProvider
  INSURANCEINTELLIGENCE_AI_PROVIDER: appAiProvider
  INSURANCEINTELLIGENCE_QUEUE_PROVIDER: appQueueProvider
  GraphMail__MailboxAccessScoped: string(graphMailboxAccessScoped)
  AzureBlob__AccountName: storageAccount.name
  AzureBlob__Endpoint: storageBlobEndpoint
  AzureBlob__DocumentContainer: claimDocumentsContainerName
  AzureBlob__KnowledgeContainer: knowledgeDocumentsContainerName
  AzureBlob__EvidenceContainer: evidencePacksContainerName
  DocumentIntelligence__Endpoint: documentIntelligenceEndpoint
  AzureAiSearch__Endpoint: searchEndpoint
  AzureAiSearch__IndexName: azureSearchIndexName
  AzureOpenAI__Endpoint: openAiEndpoint
  AzureOpenAI__DeploymentType: azureOpenAiDeploymentType
  AzureOpenAI__ChatDeployment: azureOpenAiChatDeployment
  ServiceBus__FullyQualifiedNamespace: provisionServiceBus ? serviceBusNamespaceEndpoint : ''
  ProviderHealth__TimeoutSeconds: '5'
}
output deploymentSettings object = {
  runtimeProfile: appRuntimeProfile
  deploymentSize: deploymentSize
  skuSizeProfile: skuSizeProfile
  businessLinesEnabled: businessLinesEnabled
  supportAccessMode: supportAccessMode
  privateEndpointsEnabled: privateAccessEnabled
  useManagedIdentity: useManagedIdentity
  aiProviderMode: aiProviderMode
  dataResidencyMode: dataResidencyMode
  dataResidencyCountry: dataResidencyCountry
  dataResidencyGeography: dataResidencyGeography
  dataResidencyAllowedAzureRegions: dataResidencyAllowedAzureRegions
  dataResidencyApprovalReference: dataResidencyApprovalReference
  productionResidencyConfigured: productionResidencyConfigured
  productionRegionApproved: productionRegionApproved
  documentRetentionDays: documentRetentionDays
  documentStorageMode: documentStorageMode
  documentIntelligenceMode: documentIntelligenceMode
  deployDocumentIntelligence: deployDocumentIntelligence
  knowledgeSearchMode: knowledgeSearchMode
  deployAiSearch: deployAiSearch
  azureSearchIndexName: azureSearchIndexName
  assistantAiProviderMode: assistantAiProviderMode
  deployAzureOpenAi: deployAzureOpenAi
  graphMailboxAccessScoped: graphMailboxAccessScoped
  azureOpenAiDeploymentType: azureOpenAiDeploymentType
  graphClientIdSettingName: graphClientIdSettingName
  graphSharedMailboxSettingName: graphSharedMailboxSettingName
  communicationServicesConnectionSettingName: communicationServicesConnectionSettingName
  storageProviderMode: storageProviderMode
  deployAzureSql: deployAzureSql
  managedIdentityDatabaseAccessEnabled: managedIdentityDatabaseAccessEnabled
  workflowApprovalQueueMode: workflowApprovalQueueMode
  deployServiceBus: deployServiceBus
  authMode: authMode
  auditRetentionDays: auditRetentionDays
  alertEmailOrGroup: alertEmailOrGroup
  outlookSharedMailboxEnabled: outlookSharedMailboxEnabled
  smsEmailModuleEnabled: smsEmailModuleEnabled
  teamsNotificationsEnabled: teamsNotificationsEnabled
  sharePointSyncEnabled: sharePointSyncEnabled
  voiceModuleEnabled: voiceModuleEnabled
  whatsAppModuleEnabled: whatsAppModuleEnabled
}
