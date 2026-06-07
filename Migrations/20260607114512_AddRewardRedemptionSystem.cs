using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardRedemptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RewardRedemptions",
                columns: table => new
                {
                    RewardRedemptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    RewardName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PointsCost = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedByStaffId = table.Column<int>(type: "int", nullable: true),
                    SalesTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardRedemptions", x => x.RewardRedemptionId);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_SalesTransactions_SalesTransactionId",
                        column: x => x.SalesTransactionId,
                        principalTable: "SalesTransactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_Staff_RedeemedByStaffId",
                        column: x => x.RedeemedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_CustomerId",
                table: "RewardRedemptions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_RedeemedByStaffId",
                table: "RewardRedemptions",
                column: "RedeemedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_SalesTransactionId",
                table: "RewardRedemptions",
                column: "SalesTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RewardRedemptions");
        }
    }
}
