using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierConnectionAndOfferSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "offers",
                newName: "source_supplier_connection_id");

            migrationBuilder.AddColumn<string>(
                name: "source_external_id",
                table: "offers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_import_state",
                table: "offers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "source_last_imported_at",
                table: "offers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    config_json = table.Column<string>(type: "jsonb", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_connections", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_connections_operators_operator_id",
                        column: x => x.operator_id,
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_connections_operator_id",
                table: "supplier_connections",
                column: "operator_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_connections");

            migrationBuilder.DropColumn(
                name: "source_external_id",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "source_import_state",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "source_last_imported_at",
                table: "offers");

            migrationBuilder.RenameColumn(
                name: "source_supplier_connection_id",
                table: "offers",
                newName: "supplier_id");
        }
    }
}
