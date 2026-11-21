using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Workspace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_audit_events",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tour_operators",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tour_operators", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_reservations",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tour_operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    booked_on = table.Column<DateOnly>(type: "date", nullable: false),
                    travel_start = table.Column<DateOnly>(type: "date", nullable: false),
                    travel_end = table.Column<DateOnly>(type: "date", nullable: true),
                    operator_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    customer_sale_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    cancellation_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    commission_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    commission_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    commission_base = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    commission_expected = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    commission_received = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    // Optimistic concurrency uses the xmin system column (uint Version, IsRowVersion); no column is created.
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_reservations", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_reservations_tour_operators_tour_operator_id",
                        column: x => x.tour_operator_id,
                        principalSchema: "workspace",
                        principalTable: "tour_operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_installments",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_installments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_installments_ledger_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "workspace",
                        principalTable: "ledger_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ledger_payments",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: false),
                    method = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_payments_ledger_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "workspace",
                        principalTable: "ledger_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_audit_events_agency_id_reservation_id_created_at",
                schema: "workspace",
                table: "ledger_audit_events",
                columns: new[] { "agency_id", "reservation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_installments_agency_id_due_date",
                schema: "workspace",
                table: "ledger_installments",
                columns: new[] { "agency_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_installments_reservation_id",
                schema: "workspace",
                table: "ledger_installments",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_payments_reservation_id_idempotency_key",
                schema: "workspace",
                table: "ledger_payments",
                columns: new[] { "reservation_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_reservations_agency_id_created_at",
                schema: "workspace",
                table: "ledger_reservations",
                columns: new[] { "agency_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_reservations_agency_id_status",
                schema: "workspace",
                table: "ledger_reservations",
                columns: new[] { "agency_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_reservations_agency_id_tour_operator_id_reference",
                schema: "workspace",
                table: "ledger_reservations",
                columns: new[] { "agency_id", "tour_operator_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_reservations_agency_id_travel_start",
                schema: "workspace",
                table: "ledger_reservations",
                columns: new[] { "agency_id", "travel_start" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_reservations_tour_operator_id",
                schema: "workspace",
                table: "ledger_reservations",
                column: "tour_operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_tour_operators_agency_id_name_key",
                schema: "workspace",
                table: "tour_operators",
                columns: new[] { "agency_id", "name_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_audit_events",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "ledger_installments",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "ledger_payments",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "ledger_reservations",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "tour_operators",
                schema: "workspace");
        }
    }
}
