using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrgWorkspaces",
                columns: table => new
                {
                    OrgWorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgWorkspaces", x => x.OrgWorkspaceId);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAutomationRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAutomationRuns", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAutomations",
                columns: table => new
                {
                    AutomationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrgWorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAutomations", x => x.AutomationId);
                });

            migrationBuilder.CreateTable(
                name: "PlatformInboxItems",
                columns: table => new
                {
                    InboxItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformInboxItems", x => x.InboxItemId);
                });

            migrationBuilder.CreateTable(
                name: "PlatformIntents",
                columns: table => new
                {
                    IntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformIntents", x => x.IntentId);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    SettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.SettingsId);
                });

            migrationBuilder.CreateIndex(
                name: "UX_OrgWorkspaces_Primary",
                table: "OrgWorkspaces",
                columns: new[] { "TenantId", "WorkspaceId" },
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAutomationRuns_TenantId_WorkspaceId",
                table: "PlatformAutomationRuns",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAutomationRuns_TenantId_WorkspaceId_AutomationId_StartedAt",
                table: "PlatformAutomationRuns",
                columns: new[] { "TenantId", "WorkspaceId", "AutomationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAutomations_TenantId_WorkspaceId",
                table: "PlatformAutomations",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAutomations_TenantId_WorkspaceId_OrgWorkspaceId",
                table: "PlatformAutomations",
                columns: new[] { "TenantId", "WorkspaceId", "OrgWorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAutomations_TenantId_WorkspaceId_Status",
                table: "PlatformAutomations",
                columns: new[] { "TenantId", "WorkspaceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxItems_TenantId_WorkspaceId",
                table: "PlatformInboxItems",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxItems_TenantId_WorkspaceId_DedupeKey",
                table: "PlatformInboxItems",
                columns: new[] { "TenantId", "WorkspaceId", "DedupeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxItems_TenantId_WorkspaceId_Status",
                table: "PlatformInboxItems",
                columns: new[] { "TenantId", "WorkspaceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformIntents_TenantId_WorkspaceId",
                table: "PlatformIntents",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_TenantId_WorkspaceId",
                table: "PlatformSettings",
                columns: new[] { "TenantId", "WorkspaceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrgWorkspaces");

            migrationBuilder.DropTable(
                name: "PlatformAutomationRuns");

            migrationBuilder.DropTable(
                name: "PlatformAutomations");

            migrationBuilder.DropTable(
                name: "PlatformInboxItems");

            migrationBuilder.DropTable(
                name: "PlatformIntents");

            migrationBuilder.DropTable(
                name: "PlatformSettings");
        }
    }
}
