using '../mainTemplate.bicep'

param environmentName = readEnvironmentVariable('IIP_STAGING_ENVIRONMENT_NAME', 'staging-demo')
param runtimeProfile = 'Staging'
param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus')
param insurerDisplayName = readEnvironmentVariable('INSURANCEINTELLIGENCE_INSURER_DISPLAY_NAME', 'Pilot Insurance')
param alertEmailOrGroup = readEnvironmentVariable('INSURANCEINTELLIGENCE_ALERT_EMAIL_OR_GROUP', 'ops@example.test')

param deploymentSize = 'pilot'
param skuSizeProfile = 'pilot'
param enablePrivateEndpoints = false
param privateNetworkingEnabled = false
param useManagedIdentity = true

param storageProviderMode = 'SqlServer'
param authMode = 'entra'
param documentStorageMode = 'azure_blob'
param documentIntelligenceMode = 'azure_document_intelligence'
param knowledgeSearchMode = 'azure_ai_search'
param assistantAiProviderMode = 'azure_openai'
param aiProviderMode = 'azure_openai'
param workflowApprovalQueueMode = 'service_bus'
param graphMailMode = 'microsoft_graph_mail'
param communicationServicesMode = 'disabled'
param teamsNotificationMode = 'disabled'

param deployAzureSql = true
param deployDocumentIntelligence = true
param deployAiSearch = true
param deployAzureOpenAi = false
param deployServiceBus = true

param sqlAdministratorLogin = readEnvironmentVariable('IIP_SQL_ADMIN_LOGIN', 'iiadmin')
param sqlAdministratorLoginPassword = readEnvironmentVariable('IIP_SQL_ADMIN_PASSWORD')
param sqlDatabaseName = readEnvironmentVariable('IIP_SQL_DATABASE_NAME', 'insuranceintelligence')

param portalEntraTenantId = readEnvironmentVariable('IIP_ENTRA_TENANT_ID')
param portalEntraClientId = readEnvironmentVariable('IIP_PORTAL_ENTRA_CLIENT_ID')
param portalEntraApiScope = readEnvironmentVariable('IIP_ENTRA_API_SCOPE')
param portalEntraRedirectUri = readEnvironmentVariable('IIP_ENTRA_REDIRECT_URI')

param graphMailboxAccessScoped = false

param azureOpenAiChatDeployment = readEnvironmentVariable('IIP_AZURE_OPENAI_CHAT_DEPLOYMENT')
param azureOpenAiEmbeddingDeployment = readEnvironmentVariable('IIP_AZURE_OPENAI_EMBEDDING_DEPLOYMENT')
param azureSearchIndexName = readEnvironmentVariable('IIP_AZURE_SEARCH_INDEX_NAME', 'insuranceintelligence-knowledge')

param documentRetentionDays = 365
param auditRetentionDays = 2555

param outlookSharedMailboxEnabled = true
param smsEmailModuleEnabled = false
param teamsNotificationsEnabled = false
param sharePointSyncEnabled = false
param voiceModuleEnabled = false
param whatsAppModuleEnabled = false
