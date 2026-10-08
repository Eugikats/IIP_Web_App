using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFileConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFileConnections",
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
                    table.PrimaryKey("PK_UserFileConnections", x => x.ConnectionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFileConnections_TenantId_WorkspaceId",
                table: "UserFileConnections",
                columns: new[] { "TenantId", "WorkspaceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFileConnections");
        }
    }
}
