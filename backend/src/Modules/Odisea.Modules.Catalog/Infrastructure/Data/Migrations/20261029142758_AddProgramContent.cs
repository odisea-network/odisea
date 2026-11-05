using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Catalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "catalog",
                table: "programs",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_policy",
                schema: "catalog",
                table: "programs",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "child_policy",
                schema: "catalog",
                table: "programs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "departure_point",
                schema: "catalog",
                table: "programs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "exclusions",
                schema: "catalog",
                table: "programs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "highlights",
                schema: "catalog",
                table: "programs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "inclusions",
                schema: "catalog",
                table: "programs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "insurance_policy",
                schema: "catalog",
                table: "programs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_group_size",
                schema: "catalog",
                table: "programs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_group_size",
                schema: "catalog",
                table: "programs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_terms",
                schema: "catalog",
                table: "programs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "practical_info",
                schema: "catalog",
                table: "programs",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                schema: "catalog",
                table: "programs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "summary",
                schema: "catalog",
                table: "programs",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tagline",
                schema: "catalog",
                table: "programs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                schema: "catalog",
                table: "programs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "transport_details",
                schema: "catalog",
                table: "programs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_mode",
                schema: "catalog",
                table: "programs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unspecified");

            // Programs that predate slugs get a unique placeholder; operators rename it from the content form.
            migrationBuilder.Sql("UPDATE catalog.programs SET slug = 'program-' || left(id::text, 8) WHERE slug = '';");

            migrationBuilder.CreateTable(
                name: "program_images",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_cover = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_program_images_programs_program_id",
                        column: x => x.program_id,
                        principalSchema: "catalog",
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_itinerary_days",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    meals = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    overnight = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program_itinerary_days", x => x.id);
                    table.ForeignKey(
                        name: "fk_program_itinerary_days_programs_program_id",
                        column: x => x.program_id,
                        principalSchema: "catalog",
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_programs_slug",
                schema: "catalog",
                table: "programs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_images_cover",
                schema: "catalog",
                table: "program_images",
                column: "program_id",
                unique: true,
                filter: "is_cover");

            migrationBuilder.CreateIndex(
                name: "ix_program_images_program_id_sort_order",
                schema: "catalog",
                table: "program_images",
                columns: new[] { "program_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_program_itinerary_days_program_id_day",
                schema: "catalog",
                table: "program_itinerary_days",
                columns: new[] { "program_id", "day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "program_images",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "program_itinerary_days",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_programs_slug",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "cancellation_policy",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "child_policy",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "departure_point",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "exclusions",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "highlights",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "inclusions",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "insurance_policy",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "max_group_size",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "min_group_size",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "payment_terms",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "practical_info",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "slug",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "summary",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "tagline",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "transport_details",
                schema: "catalog",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "transport_mode",
                schema: "catalog",
                table: "programs");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "catalog",
                table: "programs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000,
                oldNullable: true);
        }
    }
}
