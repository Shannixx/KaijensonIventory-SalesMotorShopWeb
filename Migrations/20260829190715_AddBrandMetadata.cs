using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Brands",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Brands",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "Brands",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Brands",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "Brands",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 1,
                columns: new[] { "CreatedAt", "CreatedBy", "Description", "SupplierId" },
                values: new object[] { new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, "Japanese motorcycle and automotive brand known for motorcycles, engines, and related mobility products.", null });

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 2,
                columns: new[] { "CreatedAt", "CreatedBy", "Description", "SupplierId" },
                values: new object[] { new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, "Japanese manufacturer known for motorcycles, engines, and a wide range of mobility products.", null });

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 3,
                columns: new[] { "CreatedAt", "CreatedBy", "Description", "SupplierId" },
                values: new object[] { new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, "Japanese manufacturer producing motorcycles, engines, and other mobility-related products.", null });

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 4,
                columns: new[] { "CreatedAt", "CreatedBy", "Description", "SupplierId" },
                values: new object[] { new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, "Japanese manufacturer known for motorcycles, engines, and other transportation and industrial products.", null });

            migrationBuilder.UpdateData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 5,
                columns: new[] { "CreatedAt", "CreatedBy", "Description", "SupplierId" },
                values: new object[] { new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, "Taiwanese manufacturer specializing in scooters, motorcycles, and related mobility products.", null });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_CreatedBy",
                table: "Brands",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_SupplierId",
                table: "Brands",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Brands_Staff_CreatedBy",
                table: "Brands",
                column: "CreatedBy",
                principalTable: "Staff",
                principalColumn: "StaffId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Brands_Suppliers_SupplierId",
                table: "Brands",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "SupplierId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Brands_Staff_CreatedBy",
                table: "Brands");

            migrationBuilder.DropForeignKey(
                name: "FK_Brands_Suppliers_SupplierId",
                table: "Brands");

            migrationBuilder.DropIndex(
                name: "IX_Brands_CreatedBy",
                table: "Brands");

            migrationBuilder.DropIndex(
                name: "IX_Brands_SupplierId",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Brands");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
