using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations;

public partial class AddCorePortPermissionTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                PermissionCode = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.PermissionCode);
            });

        migrationBuilder.CreateTable(
            name: "RoleAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                RoleCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleAssignments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                RoleCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                PermissionCode = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                RoleCode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserTenantMemberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                WorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserTenantMemberships", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RoleAssignments_TenantId_WorkspaceId_UserId",
            table: "RoleAssignments",
            columns: new[] { "TenantId", "WorkspaceId", "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_TenantId_WorkspaceId_RoleCode_PermissionCode",
            table: "RolePermissions",
            columns: new[] { "TenantId", "WorkspaceId", "RoleCode", "PermissionCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Roles_TenantId_WorkspaceId_RoleCode",
            table: "Roles",
            columns: new[] { "TenantId", "WorkspaceId", "RoleCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserTenantMemberships_TenantId_WorkspaceId_UserId",
            table: "UserTenantMemberships",
            columns: new[] { "TenantId", "WorkspaceId", "UserId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Permissions");
        migrationBuilder.DropTable(name: "RoleAssignments");
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.DropTable(name: "UserTenantMemberships");
    }
}
