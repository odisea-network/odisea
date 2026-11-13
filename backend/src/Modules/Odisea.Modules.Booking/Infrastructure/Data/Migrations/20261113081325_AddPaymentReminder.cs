using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Booking.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_reminder_sent_at",
                schema: "booking",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_reminder_sent_at",
                schema: "booking",
                table: "bookings");
        }
    }
}
