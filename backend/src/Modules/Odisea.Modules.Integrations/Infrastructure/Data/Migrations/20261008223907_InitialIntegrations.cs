using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Integrations.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integrations");

            migrationBuilder.CreateTable(
                name: "provider_call_logs",
                schema: "integrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    operation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider_call_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_call_logs_provider_code_created_at",
                schema: "integrations",
                table: "provider_call_logs",
                columns: new[] { "provider_code", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_call_logs",
                schema: "integrations");
        }
    }
}
