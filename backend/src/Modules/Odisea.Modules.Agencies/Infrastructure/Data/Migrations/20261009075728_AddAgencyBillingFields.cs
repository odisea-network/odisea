using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Agencies.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencyBillingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "billing_address",
                schema: "agencies",
                table: "agencies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "billing_city",
                schema: "agencies",
                table: "agencies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "eik",
                schema: "agencies",
                table: "agencies",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                schema: "agencies",
                table: "agencies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mol",
                schema: "agencies",
                table: "agencies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vat_number",
                schema: "agencies",
                table: "agencies",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "billing_address",
                schema: "agencies",
                table: "agencies");

            migrationBuilder.DropColumn(
                name: "billing_city",
                schema: "agencies",
                table: "agencies");

            migrationBuilder.DropColumn(
                name: "eik",
                schema: "agencies",
                table: "agencies");

            migrationBuilder.DropColumn(
                name: "legal_name",
                schema: "agencies",
                table: "agencies");

            migrationBuilder.DropColumn(
                name: "mol",
                schema: "agencies",
                table: "agencies");

            migrationBuilder.DropColumn(
                name: "vat_number",
                schema: "agencies",
                table: "agencies");
        }
    }
}
