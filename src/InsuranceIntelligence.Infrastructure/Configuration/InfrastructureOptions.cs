namespace InsuranceIntelligence.Infrastructure.Configuration;

public sealed record InfrastructureOptions
{
    public string StorageProvider { get; init; } = "InMemory";
    public string BlobProvider { get; init; } = "Mock";
    public string AuthMode { get; init; } = "Development";
    public string? RuntimeProfile { get; init; }
    public string? TenantId { get; init; }
    public string? WorkspaceId { get; init; }
    public bool AllowProductionMockProviders { get; init; }
    public bool SeedDemoData { get; init; }
    public string? SqlServerConnectionString { get; init; }
    public string? EntraTenantId { get; init; }
    public string? EntraClientId { get; init; }
    public string? EntraAudience { get; init; }
    public string? EntraIssuer { get; init; }
    /// <summary>Public origin of the customer-facing client portal (e.g. https://portal.example.com),
    /// used to build magic-link URLs sent to claimants. Required outside Development for
    /// magic-link issuance to work — see ClaimantAuthenticationEndpoints.</summary>
    public string? ClientPortalBaseUrl { get; init; }
    public string? AzureRegion { get; init; }
    public string? DataResidencyCountry { get; init; }
    public string? DataResidencyGeography { get; init; }
    public string DataResidencyMode { get; init; } = "regional";
    public string? DataResidencyAllowedAzureRegions { get; init; }
    public string? DataResidencyApprovalReference { get; init; }
    public string AiProvider { get; init; } = "Mock";
    public string DocumentProvider { get; init; } = "Mock";
    public string RetrievalProvider { get; init; } = "Mock";
    public string QueueProvider { get; init; } = "Mock";
    public string EmailProvider { get; init; } = "Mock";
    public string CommunicationProvider { get; init; } = "Mock";
    public string SmsProvider { get; init; } = "Mock";
    public string NotificationProvider { get; init; } = "Mock";
    public string ChannelProvider { get; init; } = "Mock";
    public string ProvisionerProvider { get; init; } = "Mock";
    public AzureBlobOptions AzureBlob { get; init; } = new();
    public DocumentIntelligenceOptions DocumentIntelligence { get; init; } = new();
    public AzureAiSearchOptions AzureAiSearch { get; init; } = new();
    public AzureOpenAiOptions AzureOpenAI { get; init; } = new();
    public ServiceBusOptions ServiceBus { get; init; } = new();
    public GraphMailOptions GraphMail { get; init; } = new();
    public AfricasTalkingOptions AfricasTalking { get; init; } = new();
    public WhatsAppOptions WhatsApp { get; init; } = new();
    public ProviderHealthOptions ProviderHealth { get; init; } = new();
}

public sealed record AzureBlobOptions
{
    public string? AccountName { get; init; }
    public string? Endpoint { get; init; }
    public string? ConnectionString { get; init; }
    public string DocumentContainer { get; init; } = "raw-documents";
    public string KnowledgeContainer { get; init; } = "knowledge-documents";
    public string EvidenceContainer { get; init; } = "audit-exports";
    public bool UseManagedIdentity { get; init; } = true;
}

public sealed record DocumentIntelligenceOptions
{
    public string? Endpoint { get; init; }
    public string ModelMode { get; init; } = "Prebuilt";
    public string DefaultModelId { get; init; } = "prebuilt-document";
    public bool UseManagedIdentity { get; init; } = true;
}

public sealed record AzureAiSearchOptions
{
    public string? Endpoint { get; init; }
    public string? IndexName { get; init; }
    public bool UseManagedIdentity { get; init; } = true;
    public string? SemanticConfigurationName { get; init; }
    public bool VectorSearchEnabled { get; init; }
}

public sealed record AzureOpenAiOptions
{
    public string? Endpoint { get; init; }
    public string? DeploymentType { get; init; }
    public string? ChatDeployment { get; init; }
    public string? EmbeddingDeployment { get; init; }
    public string? PolicyAnswerPromptTemplatePath { get; init; }
    public string ApiVersion { get; init; } = "2024-10-21";
    public bool UseManagedIdentity { get; init; } = true;
    public int MaxTokens { get; init; } = 800;
    public decimal Temperature { get; init; } = 0.25m;
    public int RequestTimeoutSeconds { get; init; } = 30;
}

public sealed record ServiceBusOptions
{
    public string? FullyQualifiedNamespace { get; init; }
    public bool UseManagedIdentity { get; init; } = true;
    public string AgentWorkflowWorkerUserId { get; init; } = "system:agent-workflow-worker";
    public string EmailIngestionQueue { get; init; } = "email-ingestion";
    public string DocumentProcessingQueue { get; init; } = "document-processing";
    public string KnowledgeIndexingQueue { get; init; } = "knowledge-indexing";
    public string OutboundCommunicationsQueue { get; init; } = "outbound-communications";
    public string ApprovalNotificationsQueue { get; init; } = "approval-notifications";
    public string AuditEventsQueue { get; init; } = "audit-events";
    public string AgentWorkflowQueue { get; init; } = "agent-workflow";
}

public sealed record GraphMailOptions
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? DefaultMailboxAddress { get; init; }
    public string GraphBaseUrl { get; init; } = "https://graph.microsoft.com/v1.0";
    public bool UseManagedIdentity { get; init; }
    public bool MailboxAccessScoped { get; init; }
    public bool ProcessUnreadOnly { get; init; } = true;
    public bool MoveProcessedMessages { get; init; } = true;
    public string ProcessedFolderName { get; init; } = "InsuranceIntelligence Processed";
    public int MaxMessagesPerPull { get; init; } = 10;
}

public sealed record ProviderHealthOptions
{
    public int TimeoutSeconds { get; init; } = 5;
}

public sealed record AfricasTalkingOptions
{
    public string? Username { get; init; }
    public string? ApiKey { get; init; }
    public string? SenderId { get; init; }
    public string BaseUrl { get; init; } = "https://api.africastalking.com";
    public int RequestTimeoutSeconds { get; init; } = 15;

    /// <summary>
    /// Tenant/platform opt-in for auto-sending low-risk, non-consequential transactional status SMS
    /// (e.g. "we received your claim") without individual human approval. Conservative default: OFF —
    /// every outbound SMS requires human approval until a tenant deliberately enables this. Consequential
    /// messages (claim decisions, payouts, rejections) always require approval regardless of this flag.
    /// See docs/research/sms-auto-send-policy.md.
    /// </summary>
    public bool AutoSendTransactionalSmsEnabled { get; init; }
}

/// <summary>
/// Meta WhatsApp Cloud API inbound configuration. Secrets (AccessToken, AppSecret, VerifyToken)
/// must come from Key Vault / env — never commit real values. Channel mapping fields support the
/// default one-insurer-per-Managed-App deployment; multi-number EF mapping is a later slice.
/// </summary>
public sealed record WhatsAppOptions
{
    public string? PhoneNumberId { get; init; }
    public string? AccessToken { get; init; }
    public string? AppSecret { get; init; }
    public string? VerifyToken { get; init; }
    /// <summary>Server-side tenant for this WhatsApp business number (not taken from the webhook).</summary>
    public string? TenantId { get; init; }
    /// <summary>Server-side workspace for this WhatsApp business number.</summary>
    public string? WorkspaceId { get; init; }
    /// <summary>Integration principal used for audit/actor when ingesting asynchronously.</summary>
    public string? IntegrationUserId { get; init; }
    public string GraphBaseUrl { get; init; } = "https://graph.facebook.com/v24.0";
    public int RequestTimeoutSeconds { get; init; } = 20;
}
