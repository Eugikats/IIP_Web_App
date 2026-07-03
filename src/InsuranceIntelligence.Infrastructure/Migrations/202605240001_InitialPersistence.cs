using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations;

public partial class InitialPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateScopedJsonTable(migrationBuilder, "Cases", "CaseId");
        CreateScopedJsonTable(migrationBuilder, "CaseDocuments", "DocumentId", withCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "KnowledgeDocuments", "KnowledgeDocumentId");
        CreateScopedJsonTable(migrationBuilder, "AuditEvents", "EventId", withNullableCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "AgentActionLogs", "AgentActionLogId", withNullableCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "WorkflowTasks", "TaskId", withCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "Approvals", "ApprovalId", withCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "CustomerUpdates", "UpdateId", withCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "Communications", "CommunicationId", withCaseId: true);
        CreateScopedJsonTable(migrationBuilder, "TenantConfigurations", "ConfigurationId");
        CreateScopedJsonTable(migrationBuilder, "UserAccounts", "UserAccountKey", withUserId: true);
        CreateScopedJsonTable(migrationBuilder, "MailboxConnections", "MailboxConnectionId");
        CreateScopedJsonTable(migrationBuilder, "InboundEmails", "MessageId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[]
        {
            "InboundEmails",
            "MailboxConnections",
            "UserAccounts",
            "TenantConfigurations",
            "Communications",
            "CustomerUpdates",
            "Approvals",
            "WorkflowTasks",
            "AgentActionLogs",
            "AuditEvents",
            "KnowledgeDocuments",
            "CaseDocuments",
            "Cases"
        })
        {
            migrationBuilder.DropTable(table);
        }
    }

    private static void CreateScopedJsonTable(
        MigrationBuilder migrationBuilder,
        string table,
        string idColumn,
        bool withCaseId = false,
        bool withNullableCaseId = false,
        bool withUserId = false)
    {
        migrationBuilder.CreateTable(
            name: table,
            columns: tableBuilder =>
            {
                var columns = new
                {
                    Id = tableBuilder.Column<Guid>(name: idColumn, type: "TEXT", nullable: false),
                    TenantId = tableBuilder.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    WorkspaceId = tableBuilder.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PayloadJson = tableBuilder.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = tableBuilder.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CaseId = withCaseId || withNullableCaseId
                        ? tableBuilder.Column<Guid>(type: "TEXT", nullable: withNullableCaseId)
                        : default,
                    UserId = withUserId
                        ? tableBuilder.Column<string>(type: "TEXT", nullable: false)
                        : default
                };
                return columns;
            },
            constraints: tableBuilder =>
            {
                tableBuilder.PrimaryKey($"PK_{table}", x => x.Id);
            });

        migrationBuilder.CreateIndex($"IX_{table}_TenantId_WorkspaceId", table, ["TenantId", "WorkspaceId"]);
        if (withCaseId || withNullableCaseId)
        {
            migrationBuilder.CreateIndex($"IX_{table}_TenantId_WorkspaceId_CaseId", table, ["TenantId", "WorkspaceId", "CaseId"]);
        }
    }
}
