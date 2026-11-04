using Microsoft.EntityFrameworkCore;

namespace InsuranceIntelligence.Infrastructure.Persistence;

public sealed class InsuranceIntelligenceDbContext(DbContextOptions<InsuranceIntelligenceDbContext> options) : DbContext(options)
{
    public DbSet<PersistedAggregate> Cases => Set<PersistedAggregate>();
    public DbSet<PersistedCaseDocument> CaseDocuments => Set<PersistedCaseDocument>();
    public DbSet<PersistedKnowledgeDocument> KnowledgeDocuments => Set<PersistedKnowledgeDocument>();
    public DbSet<PersistedAuditEvent> AuditEvents => Set<PersistedAuditEvent>();
    public DbSet<PersistedAgentActionLog> AgentActionLogs => Set<PersistedAgentActionLog>();
    public DbSet<PersistedWorkflowTask> WorkflowTasks => Set<PersistedWorkflowTask>();
    public DbSet<PersistedApproval> Approvals => Set<PersistedApproval>();
    public DbSet<PersistedCustomerUpdate> CustomerUpdates => Set<PersistedCustomerUpdate>();
    public DbSet<PersistedClaimReviewRequest> ClaimReviewRequests => Set<PersistedClaimReviewRequest>();
    public DbSet<PersistedContactDetailChangeRequest> ContactDetailChangeRequests => Set<PersistedContactDetailChangeRequest>();
    public DbSet<PersistedCommunication> Communications => Set<PersistedCommunication>();
    public DbSet<PersistedTenantConfiguration> TenantConfigurations => Set<PersistedTenantConfiguration>();
    public DbSet<PersistedAcademyPricing> AcademyPricings => Set<PersistedAcademyPricing>();
    public DbSet<PersistedTenantSettings> TenantSettings => Set<PersistedTenantSettings>();
    public DbSet<PersistedUserAccount> UserAccounts => Set<PersistedUserAccount>();
    public DbSet<PersistedMailboxConnection> MailboxConnections => Set<PersistedMailboxConnection>();
    public DbSet<InsuranceIntelligence.Infrastructure.ClientPortal.PersistedPortalIdentity> PortalIdentities => Set<InsuranceIntelligence.Infrastructure.ClientPortal.PersistedPortalIdentity>();
    public DbSet<PersistedInboundEmail> InboundEmails => Set<PersistedInboundEmail>();
    public DbSet<PersistedTeamsNotification> TeamsNotifications => Set<PersistedTeamsNotification>();
    public DbSet<PersistedWordingDraft> WordingDrafts => Set<PersistedWordingDraft>();
    public DbSet<PersistedSlotPdf> SlotPdfs => Set<PersistedSlotPdf>();
    public DbSet<PersistedFieldProposal> FieldProposals => Set<PersistedFieldProposal>();
    public DbSet<PersistedAuthenticityReport> AuthenticityReports => Set<PersistedAuthenticityReport>();
    public DbSet<PersistedClaimAssessment> ClaimAssessments => Set<PersistedClaimAssessment>();
    public DbSet<PersistedUploadSession> UploadSessions => Set<PersistedUploadSession>();
    public DbSet<PersistedClaimsPackDispatch> ClaimsPackDispatches => Set<PersistedClaimsPackDispatch>();
    public DbSet<PersistedClaimsPackShareLink> ClaimsPackShareLinks => Set<PersistedClaimsPackShareLink>();
    public DbSet<PersistedUploadPart> UploadParts => Set<PersistedUploadPart>();
    public DbSet<PersistedBackgroundJob> BackgroundJobs => Set<PersistedBackgroundJob>();
    public DbSet<UserTenantMembershipEntity> UserTenantMemberships => Set<UserTenantMembershipEntity>();
    public DbSet<OrganisationEntity> Organisations => Set<OrganisationEntity>();
    public DbSet<OrganisationInvitationEntity> OrganisationInvitations => Set<OrganisationInvitationEntity>();
    public DbSet<BrokerLinkEntity> BrokerLinks => Set<BrokerLinkEntity>();
    public DbSet<UsageCounterEntity> UsageCounters => Set<UsageCounterEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<PersistedOutboundWebhookSubscription> OutboundWebhookSubscriptions => Set<PersistedOutboundWebhookSubscription>();
    public DbSet<PersistedOutboundDelivery> OutboundDeliveries => Set<PersistedOutboundDelivery>();
    public DbSet<RoleAssignmentEntity> RoleAssignments => Set<RoleAssignmentEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();

    public DbSet<PersistedCustomer> Customers => Set<PersistedCustomer>();
    public DbSet<PersistedAgentState> AgentStates => Set<PersistedAgentState>();
    public DbSet<PersistedAgentWorkflowDefinition> AgentWorkflowDefinitions => Set<PersistedAgentWorkflowDefinition>();
    public DbSet<PersistedAgentWorkflowRun> AgentWorkflowRuns => Set<PersistedAgentWorkflowRun>();
    public DbSet<PersistedAgentWorkItem> AgentWorkItems => Set<PersistedAgentWorkItem>();
    public DbSet<PersistedPolicy> Policies => Set<PersistedPolicy>();
    public DbSet<PersistedCoverage> Coverages => Set<PersistedCoverage>();
    public DbSet<PersistedBranch> Branches => Set<PersistedBranch>();
    public DbSet<PersistedProducer> Producers => Set<PersistedProducer>();
    public DbSet<PersistedSalesLead> SalesLeads => Set<PersistedSalesLead>();
    public DbSet<PersistedWorkspace> Workspaces => Set<PersistedWorkspace>();
    public DbSet<PersistedTenantOnboarding> TenantOnboardings => Set<PersistedTenantOnboarding>();
    public DbSet<PersistedCommandCenterConversation> CommandCenterConversations => Set<PersistedCommandCenterConversation>();
    public DbSet<PersistedWhatsAppMessage> WhatsAppMessages => Set<PersistedWhatsAppMessage>();
    public DbSet<PersistedChannelMessage> ChannelMessages => Set<PersistedChannelMessage>();
    public DbSet<PersistedChannelIdentityLink> ChannelIdentityLinks => Set<PersistedChannelIdentityLink>();
    public DbSet<PersistedAgentRun> AgentRuns => Set<PersistedAgentRun>();
    public DbSet<PersistedNotification> Notifications => Set<PersistedNotification>();
    public DbSet<PersistedFraudSignalDisposition> FraudSignalDispositions => Set<PersistedFraudSignalDisposition>();
    public DbSet<PersistedDocumentFieldValidationRule> DocumentFieldValidationRules => Set<PersistedDocumentFieldValidationRule>();
    public DbSet<PersistedClaimantMagicLinkToken> ClaimantMagicLinkTokens => Set<PersistedClaimantMagicLinkToken>();
    public DbSet<PersistedClaimantPortalSession> ClaimantPortalSessions => Set<PersistedClaimantPortalSession>();
    public DbSet<PersistedWhatsAppChannelConfiguration> WhatsAppChannelConfigurations => Set<PersistedWhatsAppChannelConfiguration>();
    public DbSet<PersistedCaseReview> CaseReviews => Set<PersistedCaseReview>();
    public DbSet<PersistedIntegrationConnection> IntegrationConnections => Set<PersistedIntegrationConnection>();
    public DbSet<PersistedUserFileConnection> UserFileConnections => Set<PersistedUserFileConnection>();
    public DbSet<PersistedUserSecurityProfile> UserSecurityProfiles => Set<PersistedUserSecurityProfile>();
    public DbSet<PersistedMfaPolicy> MfaPolicies => Set<PersistedMfaPolicy>();
    public DbSet<PersistedLegalHold> LegalHolds => Set<PersistedLegalHold>();
    public DbSet<AcademyRecordEntity> AcademyRecords => Set<AcademyRecordEntity>();
    public DbSet<WorkspaceRecordEntity> WorkspaceRecords => Set<WorkspaceRecordEntity>();
    public DbSet<PersistedDataConnector> DataConnectors => Set<PersistedDataConnector>();
    public DbSet<PersistedConnectorRun> ConnectorRuns => Set<PersistedConnectorRun>();
    public DbSet<PersistedConnectorItem> ConnectorItems => Set<PersistedConnectorItem>();
    public DbSet<PersistedWhatsAppWebhookReceipt> WhatsAppWebhookReceipts => Set<PersistedWhatsAppWebhookReceipt>();
    public DbSet<PersistedAiUsageRecord> AiUsageRecords => Set<PersistedAiUsageRecord>();
    public DbSet<PersistedAgentBehaviourProfile> AgentBehaviourProfiles => Set<PersistedAgentBehaviourProfile>();
    public DbSet<PersistedCustomerConversationTurn> CustomerConversationTurns => Set<PersistedCustomerConversationTurn>();
    public DbSet<PersistedConsentRecord> ConsentRecords => Set<PersistedConsentRecord>();
    public DbSet<PersistedRetentionPolicy> RetentionPolicies => Set<PersistedRetentionPolicy>();
    public DbSet<PersistedProductVersion> ProductVersions => Set<PersistedProductVersion>();
    public DbSet<PersistedCommunicationTemplate> CommunicationTemplates => Set<PersistedCommunicationTemplate>();
    public DbSet<PersistedDocumentTypeDefinition> DocumentTypeDefinitions => Set<PersistedDocumentTypeDefinition>();
    public DbSet<PersistedDocumentLabelledExample> DocumentLabelledExamples => Set<PersistedDocumentLabelledExample>();
    public DbSet<PersistedRetentionReminder> RetentionReminders => Set<PersistedRetentionReminder>();
    public DbSet<PersistedSatisfactionResponse> SatisfactionResponses => Set<PersistedSatisfactionResponse>();
    public DbSet<PersistedCustomerLanguagePreference> CustomerLanguagePreferences => Set<PersistedCustomerLanguagePreference>();
    public DbSet<PersistedCustomerFollowUp> CustomerFollowUps => Set<PersistedCustomerFollowUp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureScopedJson(modelBuilder.Entity<PersistedAggregate>(), "Cases", "CaseId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCaseDocument>(), "CaseDocuments", "DocumentId");
        modelBuilder.Entity<PersistedCaseDocument>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedKnowledgeDocument>(), "KnowledgeDocuments", "KnowledgeDocumentId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedAuditEvent>(), "AuditEvents", "EventId");
        modelBuilder.Entity<PersistedAuditEvent>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentActionLog>(), "AgentActionLogs", "AgentActionLogId");
        modelBuilder.Entity<PersistedAgentActionLog>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedWorkflowTask>(), "WorkflowTasks", "TaskId");
        modelBuilder.Entity<PersistedWorkflowTask>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedApproval>(), "Approvals", "ApprovalId");
        modelBuilder.Entity<PersistedApproval>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedCustomerUpdate>(), "CustomerUpdates", "UpdateId");
        modelBuilder.Entity<PersistedCustomerUpdate>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimReviewRequest>(), "ClaimReviewRequests", "ReviewRequestId");
        modelBuilder.Entity<PersistedClaimReviewRequest>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedContactDetailChangeRequest>(), "ContactDetailChangeRequests", "RequestId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCommunication>(), "Communications", "CommunicationId");
        modelBuilder.Entity<PersistedCommunication>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedTenantConfiguration>(), "TenantConfigurations", "ConfigurationId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedAcademyPricing>(), "AcademyPricings", "AcademyPricingId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedTenantSettings>(), "TenantSettings", "TenantSettingsId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedUserAccount>(), "UserAccounts", "UserAccountKey");
        ConfigureScopedJson(modelBuilder.Entity<PersistedMailboxConnection>(), "MailboxConnections", "MailboxConnectionId");
        ConfigureScopedJson(modelBuilder.Entity<InsuranceIntelligence.Infrastructure.ClientPortal.PersistedPortalIdentity>(), "PortalIdentities", "PortalIdentityId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedInboundEmail>(), "InboundEmails", "MessageId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedTeamsNotification>(), "TeamsNotifications", "NotificationId");
        modelBuilder.Entity<PersistedTeamsNotification>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedBackgroundJob>(), "BackgroundJobs", "JobId");
        modelBuilder.Entity<PersistedBackgroundJob>().Property(entity => entity.Queue).HasMaxLength(60).IsRequired();
        modelBuilder.Entity<PersistedBackgroundJob>().Property(entity => entity.Type).HasMaxLength(60).IsRequired();
        modelBuilder.Entity<PersistedBackgroundJob>().Property(entity => entity.Key).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<PersistedBackgroundJob>().Property(entity => entity.Status).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<PersistedBackgroundJob>().Property(entity => entity.LeaseToken).IsConcurrencyToken();
        modelBuilder.Entity<PersistedBackgroundJob>().HasIndex(entity => new { entity.Queue, entity.Status, entity.NotBefore });
        modelBuilder.Entity<PersistedBackgroundJob>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Queue, entity.Type, entity.Key });
        modelBuilder.Entity<PersistedBackgroundJob>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedAuthenticityReport>(), "AuthenticityReports", "DocumentId");
        modelBuilder.Entity<PersistedAuthenticityReport>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        modelBuilder.Entity<PersistedAuthenticityReport>().Property(entity => entity.PerceptualHash).HasMaxLength(16);
        modelBuilder.Entity<PersistedAuthenticityReport>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.PerceptualHash });
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimAssessment>(), "ClaimAssessments", "AssessmentId");
        modelBuilder.Entity<PersistedClaimAssessment>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId, entity.GeneratedAt });
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimsPackDispatch>(), "ClaimsPackDispatches", "DispatchId");
        modelBuilder.Entity<PersistedClaimsPackDispatch>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimsPackShareLink>(), "ClaimsPackShareLinks", "LinkId");
        modelBuilder.Entity<PersistedClaimsPackShareLink>().Property(entity => entity.TokenHash).HasMaxLength(128).IsRequired();
        modelBuilder.Entity<PersistedClaimsPackShareLink>().HasIndex(entity => entity.TokenHash).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedUploadSession>(), "UploadSessions", "UploadId");
        modelBuilder.Entity<PersistedUploadSession>().Property(entity => entity.Fingerprint).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PersistedUploadSession>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId, entity.Fingerprint });
        ConfigureScopedJson(modelBuilder.Entity<PersistedUploadPart>(), "UploadParts", "PartId");
        modelBuilder.Entity<PersistedUploadPart>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.UploadId, entity.PartIndex });
        ConfigureScopedJson(modelBuilder.Entity<PersistedFieldProposal>(), "FieldProposals", "ProposalId");
        modelBuilder.Entity<PersistedFieldProposal>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedSlotPdf>(), "SlotPdfs", "SlotPdfId");
        modelBuilder.Entity<PersistedSlotPdf>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedWordingDraft>(), "WordingDrafts", "DraftId");
        modelBuilder.Entity<PersistedWordingDraft>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ProductId });
        ConfigurePermissionModel(modelBuilder);
        ConfigureOrganisationModel(modelBuilder);

        ConfigureScopedJson(modelBuilder.Entity<PersistedCustomer>(), "Customers", "CustomerId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentState>(), "AgentStates", "AgentStateId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentWorkflowDefinition>(), "AgentWorkflowDefinitions", "DefinitionId");
        modelBuilder.Entity<PersistedAgentWorkflowDefinition>().Property(entity => entity.WorkflowKey).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowDefinition>().Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowDefinition>().Property(entity => entity.Version).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowDefinition>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.WorkflowKey, entity.Version }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentWorkflowRun>(), "AgentWorkflowRuns", "RunId");
        modelBuilder.Entity<PersistedAgentWorkflowRun>().Property(entity => entity.WorkflowKey).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowRun>().Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowRun>().Property(entity => entity.BusinessReferenceId).HasMaxLength(240).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowRun>().Property(entity => entity.DefinitionId).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowRun>().Property(entity => entity.WorkflowVersion).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkflowRun>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.WorkflowKey, entity.Status });
        modelBuilder.Entity<PersistedAgentWorkflowRun>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.DefinitionId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentWorkItem>(), "AgentWorkItems", "WorkItemId");
        modelBuilder.Entity<PersistedAgentWorkItem>().Property(entity => entity.WorkflowKey).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkItem>().Property(entity => entity.NodeKey).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkItem>().Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedAgentWorkItem>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.RunId, entity.NodeKey });
        ConfigureScopedJson(modelBuilder.Entity<PersistedPolicy>(), "Policies", "PolicyId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCoverage>(), "Coverages", "CoverageId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedBranch>(), "Branches", "BranchId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedProducer>(), "Producers", "ProducerId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedSalesLead>(), "SalesLeads", "SalesLeadId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedWorkspace>(), "Workspaces", "WorkspaceRecordId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedTenantOnboarding>(), "TenantOnboardings", "OnboardingRecordId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCommandCenterConversation>(), "CommandCenterConversations", "ConversationId");
        modelBuilder.Entity<PersistedCommandCenterConversation>().Property(entity => entity.OwnerUserId).HasMaxLength(240).IsRequired();
        modelBuilder.Entity<PersistedCommandCenterConversation>().Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedCommandCenterConversation>().Property(entity => entity.LastActivityAt).IsRequired();
        modelBuilder.Entity<PersistedCommandCenterConversation>().Property(entity => entity.Version).IsConcurrencyToken().IsRequired();
        modelBuilder.Entity<PersistedCommandCenterConversation>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.OwnerUserId, entity.Status, entity.LastActivityAt });
        ConfigureScopedJson(modelBuilder.Entity<PersistedWhatsAppMessage>(), "WhatsAppMessages", "MessageId");
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.ThreadId).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.AccountId).HasMaxLength(240);
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.ExternalRecipientId).HasMaxLength(320);
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.ProviderMessageId).HasMaxLength(320);
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.IdempotencyKey).HasMaxLength(128);
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ThreadId, entity.CreatedAt });
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.AccountId, entity.ExternalRecipientId, entity.CreatedAt });
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ProviderMessageId }).IsUnique();
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.IdempotencyKey }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedChannelMessage>(), "ChannelMessages", "MessageId");
        modelBuilder.Entity<PersistedChannelMessage>().Property(entity => entity.Channel).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedChannelMessage>().Property(entity => entity.AccountId).HasMaxLength(240).IsRequired();
        modelBuilder.Entity<PersistedChannelMessage>().Property(entity => entity.ThreadId).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<PersistedChannelMessage>().Property(entity => entity.ExternalRecipientId).HasMaxLength(320);
        modelBuilder.Entity<PersistedChannelMessage>().Property(entity => entity.ProviderMessageId).HasMaxLength(320);
        modelBuilder.Entity<PersistedChannelMessage>().Property(entity => entity.IdempotencyKey).HasMaxLength(128);
        modelBuilder.Entity<PersistedChannelMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Channel, entity.AccountId, entity.ThreadId, entity.CreatedAt });
        modelBuilder.Entity<PersistedChannelMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Channel, entity.AccountId, entity.ExternalRecipientId, entity.CreatedAt });
        modelBuilder.Entity<PersistedChannelMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Channel, entity.AccountId, entity.ProviderMessageId }).IsUnique();
        modelBuilder.Entity<PersistedChannelMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Channel, entity.AccountId, entity.IdempotencyKey }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedChannelIdentityLink>(), "ChannelIdentityLinks", "LinkId");
        modelBuilder.Entity<PersistedChannelIdentityLink>().Property(entity => entity.CustomerId).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<PersistedChannelIdentityLink>().Property(entity => entity.Channel).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedChannelIdentityLink>().Property(entity => entity.AccountId).HasMaxLength(240).IsRequired();
        modelBuilder.Entity<PersistedChannelIdentityLink>().Property(entity => entity.ExternalRecipientId).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<PersistedChannelIdentityLink>().Property(entity => entity.IsActive).IsRequired();
        modelBuilder.Entity<PersistedChannelIdentityLink>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Channel, entity.AccountId, entity.ExternalRecipientId }).IsUnique();
        modelBuilder.Entity<PersistedChannelIdentityLink>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CustomerId, entity.IsActive });
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentRun>(), "AgentRuns", "AgentRunId");
        modelBuilder.Entity<PersistedAgentRun>().Property(entity => entity.AgentKey).HasMaxLength(80).IsRequired();
        modelBuilder.Entity<PersistedAgentRun>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.AgentKey });
        modelBuilder.Entity<PersistedAgentRun>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedFraudSignalDisposition>(), "FraudSignalDispositions", "SignalId");
        modelBuilder.Entity<PersistedFraudSignalDisposition>().Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedFraudSignalDisposition>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Status });
        ConfigureScopedJson(modelBuilder.Entity<PersistedDocumentFieldValidationRule>(), "DocumentFieldValidationRules", "RuleKey");
        modelBuilder.Entity<PersistedDocumentFieldValidationRule>().Property(entity => entity.DocumentType).HasMaxLength(60).IsRequired();
        modelBuilder.Entity<PersistedDocumentFieldValidationRule>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.DocumentType });
        ConfigureScopedJson(modelBuilder.Entity<PersistedNotification>(), "Notifications", "NotificationRecordId");
        modelBuilder.Entity<PersistedNotification>().Property(entity => entity.RecipientUserId).HasMaxLength(240).IsRequired();
        modelBuilder.Entity<PersistedNotification>().Property(entity => entity.IsRead).IsRequired();
        modelBuilder.Entity<PersistedNotification>().Property(entity => entity.CreatedAt).IsRequired();
        modelBuilder.Entity<PersistedNotification>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.RecipientUserId, entity.CreatedAt });
        modelBuilder.Entity<PersistedNotification>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.RecipientUserId, entity.IsRead });
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimantMagicLinkToken>(), "ClaimantMagicLinkTokens", "TokenId");
        // CaseId is intentionally nullable, a token verifying a policyholder with no claim yet
        // (see ClaimantAuthenticationEndpoints.RequestMagicLink) has none. Do not add
        // .IsRequired() here: EF's fluent IsRequired overrides CLR nullability inference and would
        // force this column NOT NULL despite the Guid? type, which would throw at save time for
        // exactly the case this column exists to support.
        modelBuilder.Entity<PersistedClaimantMagicLinkToken>().Property(entity => entity.TokenHash).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<PersistedClaimantMagicLinkToken>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.TokenHash }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimantPortalSession>(), "ClaimantPortalSessions", "SessionId");
        // CaseId is nullable for the same reason as PersistedClaimantMagicLinkToken above, do not
        // add .IsRequired() to it.
        modelBuilder.Entity<PersistedClaimantPortalSession>().Property(entity => entity.SessionTokenHash).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<PersistedClaimantPortalSession>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.SessionTokenHash }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedWhatsAppChannelConfiguration>(), "WhatsAppChannelConfigurations", "ConfigurationId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCaseReview>(), "CaseReviews", "CaseReviewId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedIntegrationConnection>(), "IntegrationConnections", "ConnectionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedUserFileConnection>(), "UserFileConnections", "ConnectionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedUserSecurityProfile>(), "UserSecurityProfiles", "ProfileId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedMfaPolicy>(), "MfaPolicies", "PolicyId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedLegalHold>(), "LegalHolds", "HoldId");
        modelBuilder.Entity<AcademyRecordEntity>(entity =>
        {
            entity.ToTable("AcademyRecords");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Owner).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Key).HasMaxLength(300).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.HasIndex(item => new { item.Kind, item.Owner });
            entity.HasIndex(item => new { item.Kind, item.UpdatedAt });
        });
        modelBuilder.Entity<WorkspaceRecordEntity>(entity =>
        {
            entity.ToTable("WorkspaceRecords");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkspaceId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Key).HasMaxLength(300).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.WorkspaceId, item.Kind, item.Key }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.WorkspaceId, item.Kind, item.UpdatedAt });
        });
        ConfigureScopedJson(modelBuilder.Entity<PersistedDataConnector>(), "DataConnectors", "ConnectorId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedConnectorRun>(), "ConnectorRuns", "RunId");
        modelBuilder.Entity<PersistedConnectorRun>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ConnectorId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedConnectorItem>(), "ConnectorItems", "ItemId");
        modelBuilder.Entity<PersistedConnectorItem>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ConnectorId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedWhatsAppWebhookReceipt>(), "WhatsAppWebhookReceipts", "ReceiptId");
        modelBuilder.Entity<PersistedWhatsAppWebhookReceipt>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ReceivedAt });
        ConfigureScopedJson(modelBuilder.Entity<PersistedAiUsageRecord>(), "AiUsageRecords", "UsageId");
        modelBuilder.Entity<PersistedAiUsageRecord>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.OccurredAt });
        ConfigureScopedJson(modelBuilder.Entity<PersistedOutboundWebhookSubscription>(), "OutboundWebhookSubscriptions", "SubscriptionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedOutboundDelivery>(), "OutboundDeliveries", "DeliveryId");
        modelBuilder.Entity<PersistedOutboundDelivery>().Property(entity => entity.Status).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<PersistedOutboundDelivery>().HasIndex(entity => new { entity.Status, entity.NextAttemptAt });
        modelBuilder.Entity<PersistedOutboundDelivery>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.SubscriptionId, entity.CreatedAt });
        modelBuilder.Entity<PersistedIntegrationConnection>().Property(entity => entity.ConnectionType).HasMaxLength(40);
        modelBuilder.Entity<PersistedIntegrationConnection>().Property(entity => entity.ExternalAccountId).HasMaxLength(240);
        modelBuilder.Entity<PersistedIntegrationConnection>().HasIndex(entity => new { entity.ConnectionType, entity.ExternalAccountId })
            .IsUnique().HasFilter("[ExternalAccountId] IS NOT NULL");
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentBehaviourProfile>(), "AgentBehaviourProfiles", "VersionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCustomerConversationTurn>(), "CustomerConversationTurns", "TurnId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedConsentRecord>(), "ConsentRecords", "RecordId");
        modelBuilder.Entity<PersistedConsentRecord>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CustomerIdentifier });
        ConfigureScopedJson(modelBuilder.Entity<PersistedCustomerLanguagePreference>(), "CustomerLanguagePreferences", "PreferenceId");
        modelBuilder.Entity<PersistedCustomerLanguagePreference>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ContactWaId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedCustomerFollowUp>(), "CustomerFollowUps", "TaskId");
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.CustomerId).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.Channel).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.AccountId).HasMaxLength(240).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.RecipientId).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.ThreadId).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.OriginatingMessageId).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.Status).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().Property(entity => entity.ClaimVersion).IsConcurrencyToken().IsRequired();
        modelBuilder.Entity<PersistedCustomerFollowUp>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Status, entity.DueAtUtc });
        modelBuilder.Entity<PersistedCustomerFollowUp>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Channel, entity.AccountId, entity.RecipientId, entity.Status });
        modelBuilder.Entity<PersistedCustomerFollowUp>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.OriginatingMessageId }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedRetentionReminder>(), "RetentionReminders", "ReminderId");
        modelBuilder.Entity<PersistedRetentionReminder>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.PolicyId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedSatisfactionResponse>(), "SatisfactionResponses", "ResponseId");
        modelBuilder.Entity<PersistedSatisfactionResponse>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedRetentionPolicy>(), "RetentionPolicies", "PolicyId");
        modelBuilder.Entity<PersistedCaseReview>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedProductVersion>(), "ProductVersions", "VersionId");
        modelBuilder.Entity<PersistedProductVersion>().Property(entity => entity.ProductId).IsRequired();
        modelBuilder.Entity<PersistedProductVersion>().Property(entity => entity.Version).IsRequired();
        modelBuilder.Entity<PersistedProductVersion>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ProductId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedCommunicationTemplate>(), "CommunicationTemplates", "VersionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedDocumentTypeDefinition>(), "DocumentTypeDefinitions", "VersionId");
        modelBuilder.Entity<PersistedDocumentTypeDefinition>().Property(entity => entity.Key).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PersistedDocumentTypeDefinition>().Property(entity => entity.Version).IsRequired();
        modelBuilder.Entity<PersistedDocumentTypeDefinition>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.Key });
        ConfigureScopedJson(modelBuilder.Entity<PersistedDocumentLabelledExample>(), "DocumentLabelledExamples", "ExampleId");
        modelBuilder.Entity<PersistedCommunicationTemplate>().Property(entity => entity.TemplateKey).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<PersistedCommunicationTemplate>().Property(entity => entity.Version).IsRequired();
        modelBuilder.Entity<PersistedCommunicationTemplate>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.TemplateKey });
    }

    private static void ConfigureOrganisationModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganisationEntity>(entity =>
        {
            entity.ToTable("Organisations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganisationId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DefaultWorkspaceId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.PlanId).HasMaxLength(60).IsRequired();
            entity.Property(item => item.CreatedByUserId).HasMaxLength(240).IsRequired();
            entity.Property(item => item.MarketplaceSubscriptionId).HasMaxLength(120);
            entity.Property(item => item.Product).HasMaxLength(30).IsRequired().HasDefaultValue("Insurance");
            entity.HasIndex(item => item.OrganisationId).IsUnique();
            entity.HasIndex(item => item.CreatedByUserId);
            entity.HasIndex(item => item.MarketplaceSubscriptionId);
        });

        modelBuilder.Entity<OrganisationInvitationEntity>(entity =>
        {
            entity.ToTable("OrganisationInvitations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganisationId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
            entity.Property(item => item.RoleCode).HasMaxLength(60).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.InvitedByUserId).HasMaxLength(240).IsRequired();
            entity.Property(item => item.AcceptedByUserId).HasMaxLength(240);
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.OrganisationId);
        });

        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.ToTable("ApiKeys");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OrganisationId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Prefix).HasMaxLength(16).IsRequired();
            entity.Property(item => item.KeyHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.UserId).HasMaxLength(240).IsRequired();
            entity.Property(item => item.CreatedByUserId).HasMaxLength(240).IsRequired();
            entity.HasIndex(item => item.KeyHash).IsUnique();
            entity.HasIndex(item => item.OrganisationId);
        });

        modelBuilder.Entity<UsageCounterEntity>(entity =>
        {
            entity.ToTable("UsageCounters");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Period).HasMaxLength(7).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Count).IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.Period, item.Kind }).IsUnique();
        });

        modelBuilder.Entity<BrokerLinkEntity>(entity =>
        {
            entity.ToTable("BrokerLinks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BrokerOrganisationId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.InsurerOrganisationId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.InitiatedBy).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RequestedByUserId).HasMaxLength(240).IsRequired();
            entity.Property(item => item.DecidedByUserId).HasMaxLength(240);
            entity.HasIndex(item => new { item.BrokerOrganisationId, item.Status });
            entity.HasIndex(item => new { item.InsurerOrganisationId, item.Status });
        });
    }

    private static void ConfigurePermissionModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserTenantMembershipEntity>(entity =>
        {
            entity.ToTable("UserTenantMemberships");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkspaceId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.UserId).HasMaxLength(240).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.WorkspaceId, item.UserId }).IsUnique();
        });

        modelBuilder.Entity<RoleAssignmentEntity>(entity =>
        {
            entity.ToTable("RoleAssignments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkspaceId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.UserId).HasMaxLength(240).IsRequired();
            entity.Property(item => item.RoleCode).HasMaxLength(120).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.WorkspaceId, item.UserId });
        });

        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkspaceId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.RoleCode).HasMaxLength(120).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(240).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.WorkspaceId, item.RoleCode }).IsUnique();
        });

        modelBuilder.Entity<RolePermissionEntity>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkspaceId).HasMaxLength(120).IsRequired();
            entity.Property(item => item.RoleCode).HasMaxLength(120).IsRequired();
            entity.Property(item => item.PermissionCode).HasMaxLength(160).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.WorkspaceId, item.RoleCode, item.PermissionCode }).IsUnique();
        });

        modelBuilder.Entity<PermissionEntity>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(item => item.PermissionCode);
            entity.Property(item => item.PermissionCode).HasMaxLength(160).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(240).IsRequired();
        });
    }

    private static void ConfigureScopedJson<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        string tableName,
        string keyName)
        where TEntity : PersistedScopedJson
    {
        entity.ToTable(tableName);
        entity.HasKey(item => item.Id);
        entity.Property(item => item.Id).HasColumnName(keyName);
        entity.Property(item => item.TenantId).HasMaxLength(120).IsRequired();
        entity.Property(item => item.WorkspaceId).HasMaxLength(120).IsRequired();
        entity.Property(item => item.PayloadJson).IsRequired();
        entity.HasIndex(item => new { item.TenantId, item.WorkspaceId });
    }
}

public abstract class PersistedScopedJson
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PersistedAggregate : PersistedScopedJson;
public sealed class PersistedCaseDocument : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedKnowledgeDocument : PersistedScopedJson;
public sealed class PersistedAuditEvent : PersistedScopedJson { public Guid? CaseId { get; set; } }
public sealed class PersistedAgentActionLog : PersistedScopedJson { public Guid? CaseId { get; set; } }
public sealed class PersistedWorkflowTask : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedApproval : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedCustomerUpdate : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedClaimReviewRequest : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedContactDetailChangeRequest : PersistedScopedJson { public Guid? CaseId { get; set; } }
public sealed class PersistedCommunication : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedTenantConfiguration : PersistedScopedJson;
public sealed class PersistedAcademyPricing : PersistedScopedJson;
public sealed class PersistedTenantSettings : PersistedScopedJson;
public sealed class PersistedUserAccount : PersistedScopedJson { public string UserId { get; set; } = string.Empty; }
public sealed class PersistedMailboxConnection : PersistedScopedJson;
public sealed class PersistedInboundEmail : PersistedScopedJson;
public sealed class PersistedTeamsNotification : PersistedScopedJson { public Guid? CaseId { get; set; } }
public sealed class PersistedWordingDraft : PersistedScopedJson { public Guid ProductId { get; set; } }
public sealed class PersistedSlotPdf : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedFieldProposal : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedAuthenticityReport : PersistedScopedJson { public Guid CaseId { get; set; } public string? PerceptualHash { get; set; } }
public sealed class PersistedClaimsPackDispatch : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedClaimsPackShareLink : PersistedScopedJson { public Guid CaseId { get; set; } public string TokenHash { get; set; } = string.Empty; }
public sealed class PersistedUploadSession : PersistedScopedJson { public Guid CaseId { get; set; } public string Fingerprint { get; set; } = string.Empty; }
public sealed class PersistedUploadPart : PersistedScopedJson { public Guid UploadId { get; set; } public int PartIndex { get; set; } public byte[] Data { get; set; } = []; }
public sealed class PersistedClaimAssessment : PersistedScopedJson { public Guid CaseId { get; set; } public DateTimeOffset GeneratedAt { get; set; } }
public sealed class PersistedBackgroundJob : PersistedScopedJson
{
    public string Queue { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset? LeasedUntil { get; set; }
    public Guid? CaseId { get; set; }
    public Guid LeaseToken { get; set; }
}
public sealed class PersistedCustomer : PersistedScopedJson;
public sealed class PersistedAgentState : PersistedScopedJson;
public sealed class PersistedAgentWorkflowDefinition : PersistedScopedJson
{
    public string WorkflowKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
}
public sealed class PersistedAgentWorkflowRun : PersistedScopedJson
{
    public string WorkflowKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BusinessReferenceId { get; set; } = string.Empty;
    public Guid DefinitionId { get; set; }
    public int WorkflowVersion { get; set; }
}
public sealed class PersistedAgentWorkItem : PersistedScopedJson
{
    public Guid RunId { get; set; }
    public string WorkflowKey { get; set; } = string.Empty;
    public string NodeKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class PersistedSalesLead : PersistedScopedJson;

public sealed class PersistedWorkspace : PersistedScopedJson;

public sealed class PersistedTenantOnboarding : PersistedScopedJson;
public sealed class PersistedCommandCenterConversation : PersistedScopedJson
{
    public string OwnerUserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset LastActivityAt { get; set; }
    public int Version { get; set; }
}
public sealed class PersistedWhatsAppMessage : PersistedScopedJson
{
    public Guid? CaseId { get; set; }
    public string ThreadId { get; set; } = string.Empty;
    public string? AccountId { get; set; }
    public string? ExternalRecipientId { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class PersistedChannelMessage : PersistedScopedJson
{
    public string Channel { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string? ExternalRecipientId { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class PersistedChannelIdentityLink : PersistedScopedJson
{
    public string CustomerId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string ExternalRecipientId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
public sealed class PersistedAgentRun : PersistedScopedJson
{
    public string AgentKey { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
}
public sealed class PersistedNotification : PersistedScopedJson
{
    public string RecipientUserId { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class PersistedFraudSignalDisposition : PersistedScopedJson
{
    public string Status { get; set; } = string.Empty;
}
public sealed class PersistedDocumentFieldValidationRule : PersistedScopedJson
{
    public string DocumentType { get; set; } = string.Empty;
}
public sealed class PersistedClaimantMagicLinkToken : PersistedScopedJson
{
    public Guid? CaseId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
}
public sealed class PersistedClaimantPortalSession : PersistedScopedJson
{
    public Guid? CaseId { get; set; }
    public string SessionTokenHash { get; set; } = string.Empty;
}
public sealed class PersistedWhatsAppChannelConfiguration : PersistedScopedJson;
public sealed class PersistedUserFileConnection : PersistedScopedJson;
public sealed class PersistedUserSecurityProfile : PersistedScopedJson;
public sealed class PersistedMfaPolicy : PersistedScopedJson;
public sealed class PersistedLegalHold : PersistedScopedJson;

/// <summary>One Academy record. Not scoped to an insurer's workspace: see EfAcademyRecordStore.</summary>
public sealed class AcademyRecordEntity
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
/// <summary>One small record a workspace keeps about itself (brand, templates, rules, events). Scoped to the
/// organisation and workspace that own it: see EfWorkspaceRecordStore.</summary>
public sealed class WorkspaceRecordEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class PersistedDataConnector : PersistedScopedJson;
public sealed class PersistedConnectorRun : PersistedScopedJson { public Guid ConnectorId { get; set; } }
public sealed class PersistedConnectorItem : PersistedScopedJson { public Guid ConnectorId { get; set; } }
public sealed class PersistedWhatsAppWebhookReceipt : PersistedScopedJson { public DateTimeOffset ReceivedAt { get; set; } }
public sealed class PersistedAiUsageRecord : PersistedScopedJson { public DateTimeOffset OccurredAt { get; set; } }
public sealed class PersistedOutboundWebhookSubscription : PersistedScopedJson;

public sealed class PersistedOutboundDelivery : PersistedScopedJson
{
    public Guid SubscriptionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class PersistedIntegrationConnection : PersistedScopedJson
{
    public string? ConnectionType { get; set; }
    public string? ExternalAccountId { get; set; }
}
public sealed class PersistedAgentBehaviourProfile : PersistedScopedJson { public string AgentKey { get; set; } = string.Empty; }
public sealed class PersistedCustomerConversationTurn : PersistedScopedJson { public string ThreadId { get; set; } = string.Empty; }
public sealed class PersistedConsentRecord : PersistedScopedJson { public string CustomerIdentifier { get; set; } = string.Empty; }
public sealed class PersistedCustomerLanguagePreference : PersistedScopedJson { public string ContactWaId { get; set; } = string.Empty; }
public sealed class PersistedCustomerFollowUp : PersistedScopedJson
{
    public string CustomerId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string RecipientId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string OriginatingMessageId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ClaimVersion { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? ProviderMessageId { get; set; }
    public int AttemptCount { get; set; }
    public string? SafeFailureReason { get; set; }
}
public sealed class PersistedRetentionReminder : PersistedScopedJson { public string PolicyId { get; set; } = string.Empty; }
public sealed class PersistedSatisfactionResponse : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedRetentionPolicy : PersistedScopedJson;
public sealed class PersistedCaseReview : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedProductVersion : PersistedScopedJson
{
    public Guid ProductId { get; set; }
    public int Version { get; set; }
}
public sealed class PersistedCommunicationTemplate : PersistedScopedJson
{
    public string TemplateKey { get; set; } = string.Empty;
    public int Version { get; set; }
}
public sealed class PersistedDocumentTypeDefinition : PersistedScopedJson
{
    public string Key { get; set; } = string.Empty;
    public int Version { get; set; }
}
public sealed class PersistedDocumentLabelledExample : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedPolicy : PersistedScopedJson;
public sealed class PersistedCoverage : PersistedScopedJson;
public sealed class PersistedBranch : PersistedScopedJson;
public sealed class PersistedProducer : PersistedScopedJson;

public sealed class UserTenantMembershipEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class RoleAssignmentEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
}

public sealed class RoleEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class RolePermissionEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
}

public sealed class PermissionEntity
{
    public string PermissionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
