using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    public partial class AddSerialAndPartialDelivery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsSerialized column to Products
            migrationBuilder.AddColumn<bool>(
                name: "IsSerialized",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Add ReceivedQuantity column to PurchaseOrderItems
            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Create SerialUnits table
            migrationBuilder.CreateTable(
                name: "SerialUnits",
                columns: table => new
                {
                    SerialUnitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Available"),
                    SalesTransactionId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    SoldDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerialUnits", x => x.SerialUnitId);
                    table.ForeignKey(
                        name: "FK_SerialUnits_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SerialUnits_SalesTransactions_SalesTransactionId",
                        column: x => x.SalesTransactionId,
                        principalTable: "SalesTransactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SerialUnits_SerialNumber",
                table: "SerialUnits",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SerialUnits_ProductId",
                table: "SerialUnits",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SerialUnits_SalesTransactionId",
                table: "SerialUnits",
                column: "SalesTransactionId");

            // Create DeliveryItems table
            migrationBuilder.CreateTable(
                name: "DeliveryItems",
                columns: table => new
                {
                    DeliveryItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderItemId = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryItems", x => x.DeliveryItemId);
                    table.ForeignKey(
                        name: "FK_DeliveryItems_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "Deliveries",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryItems_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "PurchaseOrderItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryItems_DeliveryId",
                table: "DeliveryItems",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryItems_PurchaseOrderItemId",
                table: "DeliveryItems",
                column: "PurchaseOrderItemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DeliveryItems");
            migrationBuilder.DropTable(name: "SerialUnits");
            migrationBuilder.DropColumn(name: "IsSerialized", table: "Products");
            migrationBuilder.DropColumn(name: "ReceivedQuantity", table: "PurchaseOrderItems");
        }
    }
}
