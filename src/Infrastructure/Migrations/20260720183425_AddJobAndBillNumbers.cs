using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAndBillNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL requires an AUTO_INCREMENT column to already be a key at the moment it's added —
            // a separate CreateIndex afterwards (EF's default codegen) errors with "there can be only
            // one auto column and it must be defined as a key". Adding the unique key in the same
            // ALTER TABLE statement satisfies that requirement.
            migrationBuilder.Sql(
                "ALTER TABLE `ServiceJobs` ADD COLUMN `JobNumber` int NOT NULL AUTO_INCREMENT, " +
                "ADD UNIQUE INDEX `IX_ServiceJobs_JobNumber` (`JobNumber`);");

            migrationBuilder.Sql(
                "ALTER TABLE `Bills` ADD COLUMN `BillNumber` int NOT NULL AUTO_INCREMENT, " +
                "ADD UNIQUE INDEX `IX_Bills_BillNumber` (`BillNumber`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobNumber",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "BillNumber",
                table: "Bills");
        }
    }
}
