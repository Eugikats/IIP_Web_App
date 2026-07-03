using '../mainTemplate.bicep'

param environmentName = 'dev'
param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus')
param insurerDisplayName = readEnvironmentVariable('INSURANCEINTELLIGENCE_INSURER_DISPLAY_NAME', 'InsuranceIntelligence Dev')
param alertEmailOrGroup = readEnvironmentVariable('INSURANCEINTELLIGENCE_ALERT_EMAIL_OR_GROUP', 'dev-ops@example.test')

param deploymentSize = 'dev'
param skuSizeProfile = 'dev'
param enablePrivateEndpoints = false
param privateNetworkingEnabled = false
param useManagedIdentity = true

param storageProviderMode = 'InMemory'
param authMode = 'development'
param documentStorageMode = 'local_mock'
param documentIntelligenceMode = 'mock'
param knowledgeSearchMode = 'mock'
param assistantAiProviderMode = 'mock'
param aiProviderMode = 'mock'
param workflowApprovalQueueMode = 'in_memory'

param deployAzureSql = false
param deployDocumentIntelligence = true
param deployAiSearch = true
param deployAzureOpenAi = false
param deployServiceBus = true

param azureSearchIndexName = 'insuranceintelligence-knowledge'
param documentRetentionDays = 365
param auditRetentionDays = 2555

param outlookSharedMailboxEnabled = false
param smsEmailModuleEnabled = false
param teamsNotificationsEnabled = false
param sharePointSyncEnabled = false
param voiceModuleEnabled = false
param whatsAppModuleEnabled = false
