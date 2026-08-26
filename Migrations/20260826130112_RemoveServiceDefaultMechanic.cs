using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServiceDefaultMechanic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_Mechanics_MechanicId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_MechanicId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "MechanicId",
                table: "Services");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MechanicId",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Services_MechanicId",
                table: "Services",
                column: "MechanicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_Mechanics_MechanicId",
                table: "Services",
                column: "MechanicId",
                principalTable: "Mechanics",
                principalColumn: "MechanicId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
