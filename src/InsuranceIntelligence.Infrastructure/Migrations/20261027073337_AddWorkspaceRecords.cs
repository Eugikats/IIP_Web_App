using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceRecords_TenantId_WorkspaceId_Kind_Key",
                table: "WorkspaceRecords",
                columns: new[] { "TenantId", "WorkspaceId", "Kind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceRecords_TenantId_WorkspaceId_Kind_UpdatedAt",
                table: "WorkspaceRecords",
                columns: new[] { "TenantId", "WorkspaceId", "Kind", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceRecords");
        }
    }
}
