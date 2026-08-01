using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductAndDeliveryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PartNumber",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "DeliveredBy",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseOrderId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_PurchaseOrderId",
                table: "Products",
                column: "PurchaseOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_PurchaseOrders_PurchaseOrderId",
                table: "Products",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "PurchaseOrderId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_PurchaseOrders_PurchaseOrderId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_PurchaseOrderId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeliveredBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DeliveredDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartNumber",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
