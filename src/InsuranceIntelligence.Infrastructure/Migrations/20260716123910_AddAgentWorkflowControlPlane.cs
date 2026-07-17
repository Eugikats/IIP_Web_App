using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWorkflowControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentStates",
                columns: table => new
                {
                    AgentStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentStates", x => x.AgentStateId);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowDefinitions",
                columns: table => new
                {
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowDefinitions", x => x.DefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BusinessReferenceId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowRuns", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkItems",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NodeKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkItems", x => x.WorkItemId);
                });

            migrationBuilder.CreateTable(
                name: "SalesLeads",
                columns: table => new
                {
                    SalesLeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLeads", x => x.SalesLeadId);
                });

            migrationBuilder.CreateTable(
                name: "TenantOnboardings",
                columns: table => new
                {
                    OnboardingRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantOnboardings", x => x.OnboardingRecordId);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    WorkspaceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.WorkspaceRecordId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentStates_TenantId_WorkspaceId",
                table: "AgentStates",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowDefinitions_TenantId_WorkspaceId",
                table: "AgentWorkflowDefinitions",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey",
                table: "AgentWorkflowDefinitions",
                columns: new[] { "TenantId", "WorkspaceId", "WorkflowKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_TenantId_WorkspaceId",
                table: "AgentWorkflowRuns",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_TenantId_WorkspaceId_WorkflowKey_Status",
                table: "AgentWorkflowRuns",
                columns: new[] { "TenantId", "WorkspaceId", "WorkflowKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkItems_TenantId_WorkspaceId",
                table: "AgentWorkItems",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkItems_TenantId_WorkspaceId_RunId_NodeKey",
                table: "AgentWorkItems",
                columns: new[] { "TenantId", "WorkspaceId", "RunId", "NodeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesLeads_TenantId_WorkspaceId",
                table: "SalesLeads",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantOnboardings_TenantId_WorkspaceId",
                table: "TenantOnboardings",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_TenantId_WorkspaceId",
                table: "Workspaces",
                columns: new[] { "TenantId", "WorkspaceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentStates");

            migrationBuilder.DropTable(
                name: "AgentWorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "AgentWorkflowRuns");

            migrationBuilder.DropTable(
                name: "AgentWorkItems");

            migrationBuilder.DropTable(
                name: "SalesLeads");

            migrationBuilder.DropTable(
                name: "TenantOnboardings");

            migrationBuilder.DropTable(
                name: "Workspaces");
        }
    }
}
