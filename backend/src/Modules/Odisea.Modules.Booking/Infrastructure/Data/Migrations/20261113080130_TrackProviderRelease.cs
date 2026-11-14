using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Booking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrackProviderRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_release",
                schema: "booking",
                table: "bookings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                // Existing rows released synchronously before this was tracked.
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "provider_release_attempts",
                schema: "booking",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "provider_release_error",
                schema: "booking",
                table: "bookings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "provider_release_updated_at",
                schema: "booking",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_provider_release",
                schema: "booking",
                table: "bookings",
                column: "provider_release",
                filter: "provider_release IN ('Pending', 'Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_provider_release",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "provider_release",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "provider_release_attempts",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "provider_release_error",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "provider_release_updated_at",
                schema: "booking",
                table: "bookings");
        }
    }
}
