using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    offers_fetched = table.Column<int>(type: "integer", nullable: false),
                    offers_imported = table.Column<int>(type: "integer", nullable: false),
                    offers_deactivated = table.Column<int>(type: "integer", nullable: false),
                    errors = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_jobs_supplier_connections_supplier_connection_id",
                        column: x => x.supplier_connection_id,
                        principalTable: "supplier_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_offers_supplier_connections_supplier_connection_id",
                        column: x => x.supplier_connection_id,
                        principalTable: "supplier_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_supplier_connection_id_started_at",
                table: "import_jobs",
                columns: new[] { "supplier_connection_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_source_offers_supplier_connection_id_external_id",
                table: "source_offers",
                columns: new[] { "supplier_connection_id", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "source_offers");
        }
    }
}
