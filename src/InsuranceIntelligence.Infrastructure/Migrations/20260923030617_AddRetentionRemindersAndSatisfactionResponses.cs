using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionRemindersAndSatisfactionResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetentionReminders",
                columns: table => new
                {
                    ReminderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionReminders", x => x.ReminderId);
                });

            migrationBuilder.CreateTable(
                name: "SatisfactionResponses",
                columns: table => new
                {
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SatisfactionResponses", x => x.ResponseId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionReminders_TenantId_WorkspaceId",
                table: "RetentionReminders",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionReminders_TenantId_WorkspaceId_PolicyId",
                table: "RetentionReminders",
                columns: new[] { "TenantId", "WorkspaceId", "PolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_SatisfactionResponses_TenantId_WorkspaceId",
                table: "SatisfactionResponses",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SatisfactionResponses_TenantId_WorkspaceId_CaseId",
                table: "SatisfactionResponses",
                columns: new[] { "TenantId", "WorkspaceId", "CaseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetentionReminders");

            migrationBuilder.DropTable(
                name: "SatisfactionResponses");
        }
    }
}
