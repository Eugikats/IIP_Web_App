using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsuranceIntelligence.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Product",
                table: "Organisations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Insurance");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Product",
                table: "Organisations");
        }
    }
}
