using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimantAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimantMagicLinkTokens",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimantMagicLinkTokens", x => x.TokenId);
                });

            migrationBuilder.CreateTable(
                name: "ClaimantPortalSessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimantPortalSessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimantMagicLinkTokens_TenantId_WorkspaceId",
                table: "ClaimantMagicLinkTokens",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimantMagicLinkTokens_TenantId_WorkspaceId_TokenHash",
                table: "ClaimantMagicLinkTokens",
                columns: new[] { "TenantId", "WorkspaceId", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimantPortalSessions_TenantId_WorkspaceId",
                table: "ClaimantPortalSessions",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimantPortalSessions_TenantId_WorkspaceId_SessionTokenHash",
                table: "ClaimantPortalSessions",
                columns: new[] { "TenantId", "WorkspaceId", "SessionTokenHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimantMagicLinkTokens");

            migrationBuilder.DropTable(
                name: "ClaimantPortalSessions");
        }
    }
}
