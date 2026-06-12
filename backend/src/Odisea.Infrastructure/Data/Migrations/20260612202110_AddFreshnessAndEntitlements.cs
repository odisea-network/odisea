using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFreshnessAndEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "freshness_ttl_hours",
                table: "supplier_connections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "operator_agency_entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    commission_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_agency_entitlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_operator_agency_entitlements_agencies_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_operator_agency_entitlements_operators_operator_id",
                        column: x => x.operator_id,
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operator_agency_entitlements_agency_id",
                table: "operator_agency_entitlements",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_operator_agency_entitlements_operator_id_agency_id",
                table: "operator_agency_entitlements",
                columns: new[] { "operator_id", "agency_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operator_agency_entitlements");

            migrationBuilder.DropColumn(
                name: "freshness_ttl_hours",
                table: "supplier_connections");
        }
    }
}
