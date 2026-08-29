using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceCategoryDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Categories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "Categories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "CreatedBy", "Description" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Products used to lubricate and maintain motorcycle engines, transmissions, and other moving components." });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "CreatedBy", "Description" },
                values: new object[] { new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Motorcycle accessories and add-on items intended to improve functionality, convenience, protection, or appearance." });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 3,
                columns: new[] { "CategoryName", "CreatedAt", "CreatedBy", "Description" },
                values: new object[] { "Spare Parts", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Replacement components used to repair, maintain, or restore motorcycles and their mechanical systems." });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CreatedBy",
                table: "Categories",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Staff_CreatedBy",
                table: "Categories",
                column: "CreatedBy",
                principalTable: "Staff",
                principalColumn: "StaffId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Staff_CreatedBy",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CreatedBy",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Categories");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 3,
                column: "CategoryName",
                value: "SpareParts");
        }
    }
}
