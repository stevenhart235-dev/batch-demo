using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatchDemo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accepted_artifact_key",
                table: "batches",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "accepted_count",
                table: "batches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_completed_at",
                table: "batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_started_at",
                table: "batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejected_artifact_key",
                table: "batches",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rejected_count",
                table: "batches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_artifact_key",
                table: "batches",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_row_count",
                table: "batches",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accepted_artifact_key",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "accepted_count",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "processing_completed_at",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "processing_started_at",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "rejected_artifact_key",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "rejected_count",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "summary_artifact_key",
                table: "batches");

            migrationBuilder.DropColumn(
                name: "total_row_count",
                table: "batches");
        }
    }
}
