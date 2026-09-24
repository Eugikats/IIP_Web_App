using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationConnections",
                columns: table => new
                {
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationConnections", x => x.ConnectionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationConnections_TenantId_WorkspaceId",
                table: "IntegrationConnections",
                columns: new[] { "TenantId", "WorkspaceId" });

            // NOTE: `dotnet ef migrations add` also generated an `IX_CaseReviews_TenantId_WorkspaceId`
            // index here, pre-existing drift between the CaseReviews model and its own migration
            // (20260906150000_AddCaseReviews only created the 3-column
            // IX_CaseReviews_TenantId_WorkspaceId_CaseId), unrelated to IntegrationConnections.
            // Deliberately left out of this migration so it stays scoped to its own change; flagged
            // separately rather than silently bundled in here.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationConnections");
        }
    }
}
