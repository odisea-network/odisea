using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Booking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentsAndStatusActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_due_at",
                schema: "booking",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);


            // Optimistic concurrency uses the xmin system column (uint Version, IsRowVersion); no column is created.
            migrationBuilder.AddColumn<string>(
                name: "actor_type",
                schema: "booking",
                table: "booking_status_history",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<Guid>(
                name: "actor_user_id",
                schema: "booking",
                table: "booking_status_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_payments",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    method = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "booking",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_status_created_at",
                schema: "booking",
                table: "bookings",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_payments_booking_id",
                schema: "booking",
                table: "booking_payments",
                column: "booking_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_payments",
                schema: "booking");

            migrationBuilder.DropIndex(
                name: "ix_bookings_status_created_at",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "payment_due_at",
                schema: "booking",
                table: "bookings");


            migrationBuilder.DropColumn(
                name: "actor_type",
                schema: "booking",
                table: "booking_status_history");

            migrationBuilder.DropColumn(
                name: "actor_user_id",
                schema: "booking",
                table: "booking_status_history");
        }
    }
}
