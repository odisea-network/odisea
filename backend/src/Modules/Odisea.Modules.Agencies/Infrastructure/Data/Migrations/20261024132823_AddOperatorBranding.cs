using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Agencies.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operator_branding",
                schema: "agencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    primary_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    accent_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    support_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    support_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    legal_footer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    logo_content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    logo_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_branding", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operator_branding",
                schema: "agencies");
        }
    }
}
