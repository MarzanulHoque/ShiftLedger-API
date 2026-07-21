using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShiftLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V2_DepartmentsAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "ServiceJobs",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "UnpaidAlertDays",
                table: "OrgSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Name", "RowVersion" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "Mechanics", new Guid("00000000-0000-0000-0000-0000000000b1") },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "Bike Wash", new Guid("00000000-0000-0000-0000-0000000000b2") }
                });

            migrationBuilder.UpdateData(
                table: "OrgSettings",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "UnpaidAlertDays",
                value: 7);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_DepartmentId",
                table: "ServiceJobs",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobs_Departments_DepartmentId",
                table: "ServiceJobs",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobs_Departments_DepartmentId",
                table: "ServiceJobs");

            migrationBuilder.DropIndex(
                name: "IX_ServiceJobs_DepartmentId",
                table: "ServiceJobs");

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"));

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"));

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "UnpaidAlertDays",
                table: "OrgSettings");
        }
    }
}
