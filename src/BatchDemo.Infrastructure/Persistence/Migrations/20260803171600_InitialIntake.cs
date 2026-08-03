using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatchDemo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_object_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    original_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    canonical_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batches", x => x.batch_id);
                    table.CheckConstraint("ck_batches_duplicate_canonical", "(status = 'Duplicate' AND canonical_batch_id IS NOT NULL) OR (status <> 'Duplicate' AND canonical_batch_id IS NULL)");
                    table.CheckConstraint("ck_batches_sha256", "length(original_sha256) = 64");
                    table.ForeignKey(
                        name: "fk_batches_canonical_batch",
                        column: x => x.canonical_batch_id,
                        principalTable: "batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batch_work_items",
                columns: table => new
                {
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_work_items", x => x.work_item_id);
                    table.CheckConstraint("ck_batch_work_items_attempt_count", "attempt_count >= 0");
                    table.ForeignKey(
                        name: "fk_batch_work_items_batch",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_batch_work_items_available",
                table: "batch_work_items",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "ux_batch_work_items_batch_id",
                table: "batch_work_items",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_batches_canonical_batch_id",
                table: "batches",
                column: "canonical_batch_id");

            migrationBuilder.CreateIndex(
                name: "ux_batches_canonical_delivery",
                table: "batches",
                columns: new[] { "merchant_id", "original_sha256" },
                unique: true,
                filter: "canonical_batch_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_batches_original_object_key",
                table: "batches",
                column: "original_object_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_work_items");

            migrationBuilder.DropTable(
                name: "batches");
        }
    }
}
