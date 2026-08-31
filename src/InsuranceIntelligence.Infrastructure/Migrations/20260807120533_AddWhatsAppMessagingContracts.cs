using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppMessagingContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThreadId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessages", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_WorkspaceId",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_WorkspaceId_IdempotencyKey",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "WorkspaceId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_WorkspaceId_ProviderMessageId",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "WorkspaceId", "ProviderMessageId" },
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_WorkspaceId_ThreadId_CreatedAt",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "WorkspaceId", "ThreadId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppMessages");
        }
    }
}
