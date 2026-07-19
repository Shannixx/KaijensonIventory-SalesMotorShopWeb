using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCategoryBrandSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Brands");

            migrationBuilder.AddColumn<string>(
                name: "CountryOrigin",
                table: "Brands",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "BrandId", "BrandName", "CountryOrigin", "Status" },
                values: new object[,]
                {
                    { 1, "Honda", "Japan", "Active" },
                    { 2, "Yamaha", "Japan", "Active" },
                    { 3, "Suzuki", "Japan", "Active" },
                    { 4, "Kawasaki", "Japan", "Active" },
                    { 5, "Kymco", "Taiwan", "Active" }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "CategoryId", "CategoryName" },
                values: new object[,]
                {
                    { 1, "Lubricant" },
                    { 2, "Accessories" },
                    { 3, "SpareParts" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "BrandId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "CategoryId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "CountryOrigin",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Brands");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Brands",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }
    }
}
