using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Booking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendBookingPassengers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "adults",
                schema: "booking",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "children",
                schema: "booking",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Rows that predate occupancy tracking: one adult per passenger, at least one.
            migrationBuilder.Sql(
                "UPDATE booking.bookings b " +
                "SET adults = GREATEST(1, (SELECT count(*) FROM booking.booking_passengers p WHERE p.booking_id = b.id));");

            migrationBuilder.AddColumn<DateOnly>(
                name: "document_expiry_date",
                schema: "booking",
                table: "booking_passengers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_issuing_country",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_type",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nationality",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "position",
                schema: "booking",
                table: "booking_passengers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "booking",
                table: "booking_passengers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            // Pre-existing passengers get a stable order: lead first, then creation order.
            migrationBuilder.Sql(
                "UPDATE booking.booking_passengers p SET position = sub.rn - 1 " +
                "FROM (SELECT id, row_number() OVER (PARTITION BY booking_id ORDER BY is_lead DESC, created_at) AS rn " +
                "      FROM booking.booking_passengers) sub " +
                "WHERE sub.id = p.id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adults",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "children",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "document_expiry_date",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "document_issuing_country",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "document_number",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "document_type",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "gender",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "nationality",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "position",
                schema: "booking",
                table: "booking_passengers");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "booking",
                table: "booking_passengers");
        }
    }
}
