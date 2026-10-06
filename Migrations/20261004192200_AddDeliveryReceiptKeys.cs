using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004192200_AddDeliveryReceiptKeys")]
    public sealed class AddDeliveryReceiptKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Historical receipts remain untouched (their keys are NULL).
            migrationBuilder.AddColumn<string>(
                name: "ReceiptKey",
                table: "DeliveryItems",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryItems_DeliveryId_PurchaseOrderItemId_ReceiptKey",
                table: "DeliveryItems",
                columns: new[] { "DeliveryId", "PurchaseOrderItemId", "ReceiptKey" },
                unique: true,
                filter: "[ReceiptKey] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryItems_DeliveryId_PurchaseOrderItemId_ReceiptKey",
                table: "DeliveryItems");
            migrationBuilder.DropColumn(name: "ReceiptKey", table: "DeliveryItems");
        }
    }
}
