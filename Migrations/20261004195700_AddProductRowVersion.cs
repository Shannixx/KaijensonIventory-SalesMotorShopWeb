using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004195700_AddProductRowVersion")]
    public sealed class AddProductRowVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server initializes the revision for every existing product row.
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RowVersion", table: "Products");
        }
    }
}
