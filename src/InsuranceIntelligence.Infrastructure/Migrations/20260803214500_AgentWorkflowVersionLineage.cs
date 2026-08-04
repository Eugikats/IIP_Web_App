using System;
using InsuranceIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(InsuranceIntelligenceDbContext))]
    [Migration("20260803214500_AgentWorkflowVersionLineage")]
    public partial class AgentWorkflowVersionLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey",
                table: "AgentWorkflowDefinitions");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AgentWorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE AgentWorkflowDefinitions
                SET Version = COALESCE(
                    TRY_CAST(JSON_VALUE(PayloadJson, '$.version') AS int),
                    TRY_CAST(JSON_VALUE(PayloadJson, '$.Version') AS int),
                    1);
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "DefinitionId",
                table: "AgentWorkflowRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "WorkflowVersion",
                table: "AgentWorkflowRuns",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE AgentWorkflowRuns
                SET DefinitionId = COALESCE(
                        TRY_CONVERT(uniqueidentifier, JSON_VALUE(PayloadJson, '$.definitionId')),
                        TRY_CONVERT(uniqueidentifier, JSON_VALUE(PayloadJson, '$.DefinitionId')),
                        DefinitionId),
                    WorkflowVersion = COALESCE(
                        TRY_CAST(JSON_VALUE(PayloadJson, '$.workflowVersion') AS int),
                        TRY_CAST(JSON_VALUE(PayloadJson, '$.WorkflowVersion') AS int),
                        1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey_Version",
                table: "AgentWorkflowDefinitions",
                columns: new[] { "TenantId", "WorkspaceId", "WorkflowKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_TenantId_WorkspaceId_DefinitionId",
                table: "AgentWorkflowRuns",
                columns: new[] { "TenantId", "WorkspaceId", "DefinitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowRuns_TenantId_WorkspaceId_DefinitionId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey_Version",
                table: "AgentWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "WorkflowVersion",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "DefinitionId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AgentWorkflowDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowDefinitions_TenantId_WorkspaceId_WorkflowKey",
                table: "AgentWorkflowDefinitions",
                columns: new[] { "TenantId", "WorkspaceId", "WorkflowKey" },
                unique: true);
        }
    }
}
