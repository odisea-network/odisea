using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odisea.Modules.Workspace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInquiryForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inquiry_forms",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    access_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    allowed_origins = table.Column<string[]>(type: "text[]", nullable: false),
                    notify_emails = table.Column<string[]>(type: "text[]", nullable: false),
                    auto_reply_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    auto_reply_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    auto_reply_body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    email_field = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name_field = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    honeypot_field = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    submission_count = table.Column<int>(type: "integer", nullable: false),
                    spam_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inquiry_forms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inquiry_submissions",
                schema: "workspace",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fields = table.Column<string>(type: "jsonb", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    customer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origin = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    internal_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inquiry_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_inquiry_submissions_inquiry_forms_form_id",
                        column: x => x.form_id,
                        principalSchema: "workspace",
                        principalTable: "inquiry_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inquiry_forms_access_key",
                schema: "workspace",
                table: "inquiry_forms",
                column: "access_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inquiry_forms_agency_id",
                schema: "workspace",
                table: "inquiry_forms",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_inquiry_submissions_agency_id_status_created_at",
                schema: "workspace",
                table: "inquiry_submissions",
                columns: new[] { "agency_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inquiry_submissions_created_at",
                schema: "workspace",
                table: "inquiry_submissions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_inquiry_submissions_form_id",
                schema: "workspace",
                table: "inquiry_submissions",
                column: "form_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inquiry_submissions",
                schema: "workspace");

            migrationBuilder.DropTable(
                name: "inquiry_forms",
                schema: "workspace");
        }
    }
}
