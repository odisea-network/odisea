using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Agencies.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAgencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agencies");

            migrationBuilder.CreateTable(
                name: "agencies",
                schema: "agencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    commission_level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "commission_levels",
                schema: "agencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operator_users",
                schema: "agencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_out_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agency_users",
                schema: "agencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_out_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agency_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_agency_users_agencies_agency_id",
                        column: x => x.agency_id,
                        principalSchema: "agencies",
                        principalTable: "agencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agencies_name",
                schema: "agencies",
                table: "agencies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_agency_users_agency_id",
                schema: "agencies",
                table: "agency_users",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_agency_users_email",
                schema: "agencies",
                table: "agency_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operator_users_email",
                schema: "agencies",
                table: "operator_users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agency_users",
                schema: "agencies");

            migrationBuilder.DropTable(
                name: "commission_levels",
                schema: "agencies");

            migrationBuilder.DropTable(
                name: "operator_users",
                schema: "agencies");

            migrationBuilder.DropTable(
                name: "agencies",
                schema: "agencies");
        }
    }
}
