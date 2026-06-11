using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    departure_date = table.Column<DateOnly>(type: "date", nullable: true),
                    duration_nights = table.Column<int>(type: "integer", nullable: true),
                    board_basis = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    occupancy = table.Column<int>(type: "integer", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_variants", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_variants_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_variants_offer_id_board_basis",
                table: "price_variants",
                columns: new[] { "offer_id", "board_basis" });

            migrationBuilder.CreateIndex(
                name: "ix_price_variants_offer_id_departure_date",
                table: "price_variants",
                columns: new[] { "offer_id", "departure_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_variants");
        }
    }
}
