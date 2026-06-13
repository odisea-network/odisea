using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeIsPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_preset",
                table: "themes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_preset",
                table: "themes");
        }
    }
}
