using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Agencies.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "plan",
                schema: "agencies",
                table: "agencies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "plan",
                schema: "agencies",
                table: "agencies");
        }
    }
}
