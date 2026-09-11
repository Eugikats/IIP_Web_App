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
    public DbSet<PersistedCommunication> Communications => Set<PersistedCommunication>();
    public DbSet<PersistedTenantConfiguration> TenantConfigurations => Set<PersistedTenantConfiguration>();
    public DbSet<PersistedUserAccount> UserAccounts => Set<PersistedUserAccount>();
    public DbSet<PersistedMailboxConnection> MailboxConnections => Set<PersistedMailboxConnection>();
    public DbSet<PersistedInboundEmail> InboundEmails => Set<PersistedInboundEmail>();
    public DbSet<PersistedTeamsNotification> TeamsNotifications => Set<PersistedTeamsNotification>();
    public DbSet<UserTenantMembershipEntity> UserTenantMemberships => Set<UserTenantMembershipEntity>();
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
    public DbSet<PersistedAgentRun> AgentRuns => Set<PersistedAgentRun>();
    public DbSet<PersistedNotification> Notifications => Set<PersistedNotification>();
    public DbSet<PersistedFraudSignalDisposition> FraudSignalDispositions => Set<PersistedFraudSignalDisposition>();
    public DbSet<PersistedDocumentFieldValidationRule> DocumentFieldValidationRules => Set<PersistedDocumentFieldValidationRule>();
    public DbSet<PersistedClaimantMagicLinkToken> ClaimantMagicLinkTokens => Set<PersistedClaimantMagicLinkToken>();
    public DbSet<PersistedClaimantPortalSession> ClaimantPortalSessions => Set<PersistedClaimantPortalSession>();
    public DbSet<PersistedWhatsAppChannelConfiguration> WhatsAppChannelConfigurations => Set<PersistedWhatsAppChannelConfiguration>();
    public DbSet<PersistedCaseReview> CaseReviews => Set<PersistedCaseReview>();
    public DbSet<PersistedIntegrationConnection> IntegrationConnections => Set<PersistedIntegrationConnection>();
    public DbSet<PersistedAgentBehaviourProfile> AgentBehaviourProfiles => Set<PersistedAgentBehaviourProfile>();
    public DbSet<PersistedCustomerConversationTurn> CustomerConversationTurns => Set<PersistedCustomerConversationTurn>();
    public DbSet<PersistedConsentRecord> ConsentRecords => Set<PersistedConsentRecord>();

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
        ConfigureScopedJson(modelBuilder.Entity<PersistedCommunication>(), "Communications", "CommunicationId");
        modelBuilder.Entity<PersistedCommunication>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigureScopedJson(modelBuilder.Entity<PersistedTenantConfiguration>(), "TenantConfigurations", "ConfigurationId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedUserAccount>(), "UserAccounts", "UserAccountKey");
        ConfigureScopedJson(modelBuilder.Entity<PersistedMailboxConnection>(), "MailboxConnections", "MailboxConnectionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedInboundEmail>(), "InboundEmails", "MessageId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedTeamsNotification>(), "TeamsNotifications", "NotificationId");
        modelBuilder.Entity<PersistedTeamsNotification>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
        ConfigurePermissionModel(modelBuilder);

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
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.ProviderMessageId).HasMaxLength(320);
        modelBuilder.Entity<PersistedWhatsAppMessage>().Property(entity => entity.IdempotencyKey).HasMaxLength(128);
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ThreadId, entity.CreatedAt });
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.ProviderMessageId }).IsUnique();
        modelBuilder.Entity<PersistedWhatsAppMessage>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.IdempotencyKey }).IsUnique();
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
        // CaseId is intentionally nullable — a token verifying a policyholder with no claim yet
        // (see ClaimantAuthenticationEndpoints.RequestMagicLink) has none. Do not add
        // .IsRequired() here: EF's fluent IsRequired overrides CLR nullability inference and would
        // force this column NOT NULL despite the Guid? type, which would throw at save time for
        // exactly the case this column exists to support.
        modelBuilder.Entity<PersistedClaimantMagicLinkToken>().Property(entity => entity.TokenHash).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<PersistedClaimantMagicLinkToken>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.TokenHash }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedClaimantPortalSession>(), "ClaimantPortalSessions", "SessionId");
        // CaseId is nullable for the same reason as PersistedClaimantMagicLinkToken above — do not
        // add .IsRequired() to it.
        modelBuilder.Entity<PersistedClaimantPortalSession>().Property(entity => entity.SessionTokenHash).HasMaxLength(64).IsRequired();
        modelBuilder.Entity<PersistedClaimantPortalSession>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.SessionTokenHash }).IsUnique();
        ConfigureScopedJson(modelBuilder.Entity<PersistedWhatsAppChannelConfiguration>(), "WhatsAppChannelConfigurations", "ConfigurationId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCaseReview>(), "CaseReviews", "CaseReviewId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedIntegrationConnection>(), "IntegrationConnections", "ConnectionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedAgentBehaviourProfile>(), "AgentBehaviourProfiles", "VersionId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCustomerConversationTurn>(), "CustomerConversationTurns", "TurnId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedConsentRecord>(), "ConsentRecords", "RecordId");
        modelBuilder.Entity<PersistedConsentRecord>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CustomerIdentifier });
        modelBuilder.Entity<PersistedCaseReview>().HasIndex(entity => new { entity.TenantId, entity.WorkspaceId, entity.CaseId });
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
public sealed class PersistedCommunication : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedTenantConfiguration : PersistedScopedJson;
public sealed class PersistedUserAccount : PersistedScopedJson { public string UserId { get; set; } = string.Empty; }
public sealed class PersistedMailboxConnection : PersistedScopedJson;
public sealed class PersistedInboundEmail : PersistedScopedJson;
public sealed class PersistedTeamsNotification : PersistedScopedJson { public Guid? CaseId { get; set; } }
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
    public string? ProviderMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
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
public sealed class PersistedIntegrationConnection : PersistedScopedJson;
public sealed class PersistedAgentBehaviourProfile : PersistedScopedJson { public string AgentKey { get; set; } = string.Empty; }
public sealed class PersistedCustomerConversationTurn : PersistedScopedJson { public string ThreadId { get; set; } = string.Empty; }
public sealed class PersistedConsentRecord : PersistedScopedJson { public string CustomerIdentifier { get; set; } = string.Empty; }
public sealed class PersistedCaseReview : PersistedScopedJson { public Guid CaseId { get; set; } }
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
