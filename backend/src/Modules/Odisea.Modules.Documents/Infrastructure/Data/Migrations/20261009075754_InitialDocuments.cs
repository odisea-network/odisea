using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Documents.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "company_profiles",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    eik = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    vat_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mol = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_counters",
                schema: "documents",
                columns: table => new
                {
                    series = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_counters", x => x.series);
                });

            migrationBuilder.CreateTable(
                name: "financial_documents",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    tax_event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_eik = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    supplier_vat_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    supplier_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    supplier_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_mol = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    supplier_bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_eik = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    recipient_vat_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    recipient_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    recipient_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipient_mol = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booking_ref = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    related_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_document_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    related_document_date = table.Column<DateOnly>(type: "date", nullable: true),
                    vat_treatment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_rate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tax_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    legal_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    annulment_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_documents_agency_id_issue_date",
                schema: "documents",
                table: "financial_documents",
                columns: new[] { "agency_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_documents_booking_id",
                schema: "documents",
                table: "financial_documents",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_documents_number",
                schema: "documents",
                table: "financial_documents",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_profiles",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_counters",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "financial_documents",
                schema: "documents");
        }
    }
}
