using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Documents.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contract_templates",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    file_size = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    placeholders = table.Column<string[]>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    agency_usable = table.Column<bool>(type: "boolean", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contract_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generated_contracts",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booking_ref = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    file_size = table.Column<int>(type: "integer", nullable: false),
                    generated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_by_agency = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    shared_with_agency = table.Column<bool>(type: "boolean", nullable: false),
                    shared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generated_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_generated_contracts_contract_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "documents",
                        principalTable: "contract_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contract_templates_is_active_agency_usable",
                schema: "documents",
                table: "contract_templates",
                columns: new[] { "is_active", "agency_usable" });

            migrationBuilder.CreateIndex(
                name: "ix_contract_templates_kind_is_active",
                schema: "documents",
                table: "contract_templates",
                columns: new[] { "kind", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_generated_contracts_agency_id",
                schema: "documents",
                table: "generated_contracts",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_contracts_booking_id",
                schema: "documents",
                table: "generated_contracts",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_contracts_template_id",
                schema: "documents",
                table: "generated_contracts",
                column: "template_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_contracts",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "contract_templates",
                schema: "documents");
        }
    }
}
