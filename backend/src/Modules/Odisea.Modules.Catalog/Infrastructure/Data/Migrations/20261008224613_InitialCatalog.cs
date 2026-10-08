using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Catalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "destinations",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_destinations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hotels",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotels", x => x.id);
                    table.ForeignKey(
                        name: "fk_hotels_destinations_destination_id",
                        column: x => x.destination_id,
                        principalSchema: "catalog",
                        principalTable: "destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "location_mappings",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_location_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_mappings_destinations_destination_id",
                        column: x => x.destination_id,
                        principalSchema: "catalog",
                        principalTable: "destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "programs",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    season = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_programs", x => x.id);
                    table.ForeignKey(
                        name: "fk_programs_destinations_destination_id",
                        column: x => x.destination_id,
                        principalSchema: "catalog",
                        principalTable: "destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hotel_mappings",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_hotel_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hotel_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_hotel_mappings_hotels_hotel_id",
                        column: x => x.hotel_id,
                        principalSchema: "catalog",
                        principalTable: "hotels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_departures",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    transport_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program_departures", x => x.id);
                    table.ForeignKey(
                        name: "fk_program_departures_programs_program_id",
                        column: x => x.program_id,
                        principalSchema: "catalog",
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_hotels",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program_hotels", x => x.id);
                    table.ForeignKey(
                        name: "fk_program_hotels_hotels_hotel_id",
                        column: x => x.hotel_id,
                        principalSchema: "catalog",
                        principalTable: "hotels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_program_hotels_programs_program_id",
                        column: x => x.program_id,
                        principalSchema: "catalog",
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_destinations_name_country",
                schema: "catalog",
                table: "destinations",
                columns: new[] { "name", "country" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hotel_mappings_hotel_id_provider_code",
                schema: "catalog",
                table: "hotel_mappings",
                columns: new[] { "hotel_id", "provider_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hotel_mappings_provider_code_external_hotel_code",
                schema: "catalog",
                table: "hotel_mappings",
                columns: new[] { "provider_code", "external_hotel_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hotels_destination_id",
                schema: "catalog",
                table: "hotels",
                column: "destination_id");

            migrationBuilder.CreateIndex(
                name: "ix_location_mappings_destination_id_provider_code",
                schema: "catalog",
                table: "location_mappings",
                columns: new[] { "destination_id", "provider_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_location_mappings_provider_code_external_location_code",
                schema: "catalog",
                table: "location_mappings",
                columns: new[] { "provider_code", "external_location_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_departures_program_id_start_date",
                schema: "catalog",
                table: "program_departures",
                columns: new[] { "program_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_program_hotels_hotel_id",
                schema: "catalog",
                table: "program_hotels",
                column: "hotel_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_hotels_program_id_hotel_id",
                schema: "catalog",
                table: "program_hotels",
                columns: new[] { "program_id", "hotel_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programs_destination_id",
                schema: "catalog",
                table: "programs",
                column: "destination_id");

            migrationBuilder.CreateIndex(
                name: "ix_programs_status",
                schema: "catalog",
                table: "programs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hotel_mappings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "location_mappings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "program_departures",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "program_hotels",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "hotels",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "programs",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "destinations",
                schema: "catalog");
        }
    }
}
