using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Workspace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_documents",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_documents_ledger_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "workspace",
                        principalTable: "ledger_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ledger_documents_ledger_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "workspace",
                        principalTable: "ledger_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_documents_agency_id_reservation_id",
                schema: "workspace",
                table: "ledger_documents",
                columns: new[] { "agency_id", "reservation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_documents_payment_id",
                schema: "workspace",
                table: "ledger_documents",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_documents_reservation_id",
                schema: "workspace",
                table: "ledger_documents",
                column: "reservation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_documents",
                schema: "workspace");
        }
    }
}
