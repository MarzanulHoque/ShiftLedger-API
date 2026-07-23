using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_NotificationAlertTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastOverdueAlertAtUtc",
                table: "ServiceJobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Bills",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUnpaidAlertAtUtc",
                table: "Bills",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastOverdueAlertAtUtc",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "LastUnpaidAlertAtUtc",
                table: "Bills");
        }
    }
}
