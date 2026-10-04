using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerOrganisationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InsurerOrganisationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InitiatedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    RoleCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedByUserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultWorkspaceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlanId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MarketplaceSubscriptionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerLinks_BrokerOrganisationId_Status",
                table: "BrokerLinks",
                columns: new[] { "BrokerOrganisationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerLinks_InsurerOrganisationId_Status",
                table: "BrokerLinks",
                columns: new[] { "InsurerOrganisationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationInvitations_OrganisationId",
                table: "OrganisationInvitations",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationInvitations_TokenHash",
                table: "OrganisationInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_CreatedByUserId",
                table: "Organisations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_MarketplaceSubscriptionId",
                table: "Organisations",
                column: "MarketplaceSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_OrganisationId",
                table: "Organisations",
                column: "OrganisationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerLinks");

            migrationBuilder.DropTable(
                name: "OrganisationInvitations");

            migrationBuilder.DropTable(
                name: "Organisations");
        }
    }
}
