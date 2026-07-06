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
    public DbSet<PersistedPolicy> Policies => Set<PersistedPolicy>();
    public DbSet<PersistedCoverage> Coverages => Set<PersistedCoverage>();
    public DbSet<PersistedBranch> Branches => Set<PersistedBranch>();
    public DbSet<PersistedProducer> Producers => Set<PersistedProducer>();
    public DbSet<PersistedSalesLead> SalesLeads => Set<PersistedSalesLead>();

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
        ConfigureScopedJson(modelBuilder.Entity<PersistedPolicy>(), "Policies", "PolicyId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedCoverage>(), "Coverages", "CoverageId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedBranch>(), "Branches", "BranchId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedProducer>(), "Producers", "ProducerId");
        ConfigureScopedJson(modelBuilder.Entity<PersistedSalesLead>(), "SalesLeads", "SalesLeadId");
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
public sealed class PersistedCommunication : PersistedScopedJson { public Guid CaseId { get; set; } }
public sealed class PersistedTenantConfiguration : PersistedScopedJson;
public sealed class PersistedUserAccount : PersistedScopedJson { public string UserId { get; set; } = string.Empty; }
public sealed class PersistedMailboxConnection : PersistedScopedJson;
public sealed class PersistedInboundEmail : PersistedScopedJson;
public sealed class PersistedTeamsNotification : PersistedScopedJson { public Guid? CaseId { get; set; } }
public sealed class PersistedCustomer : PersistedScopedJson;
public sealed class PersistedAgentState : PersistedScopedJson;

public sealed class PersistedSalesLead : PersistedScopedJson;
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
