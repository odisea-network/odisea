using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Workspace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workspace");

            migrationBuilder.CreateTable(
                name: "external_reservations",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tracking_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    customer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    customer_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    travel_from = table.Column<DateOnly>(type: "date", nullable: true),
                    travel_to = table.Column<DateOnly>(type: "date", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_reservations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "external_reservation_events",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_reservation_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_reservation_events_external_reservations_reservati",
                        column: x => x.reservation_id,
                        principalSchema: "workspace",
                        principalTable: "external_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_reservation_events_reservation_id",
                schema: "workspace",
                table: "external_reservation_events",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_reservations_agency_id_reference",
                schema: "workspace",
                table: "external_reservations",
                columns: new[] { "agency_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_reservations_agency_id_status",
                schema: "workspace",
                table: "external_reservations",
                columns: new[] { "agency_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_external_reservations_agency_id_updated_at",
                schema: "workspace",
                table: "external_reservations",
                columns: new[] { "agency_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_external_reservations_created_at",
                schema: "workspace",
                table: "external_reservations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_external_reservations_tracking_code",
                schema: "workspace",
                table: "external_reservations",
                column: "tracking_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_reservation_events",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "external_reservations",
                schema: "workspace");
        }
    }
}
