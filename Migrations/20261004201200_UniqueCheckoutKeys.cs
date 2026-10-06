using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004201200_UniqueCheckoutKeys")]
    public sealed class UniqueCheckoutKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Never discard or rewrite historical sales to make this constraint fit.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM dbo.SalesTransactions
                    GROUP BY CheckoutKey HAVING COUNT_BIG(*) > 1
                ) THROW 51004, 'Existing duplicate checkout keys require manual reconciliation before migration.', 1;");

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransactions_CheckoutKey",
                table: "SalesTransactions",
                column: "CheckoutKey",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_SalesTransactions_CheckoutKey", table: "SalesTransactions");
        }
    }
}
