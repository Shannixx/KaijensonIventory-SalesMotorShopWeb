using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedByStaffToServiceJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessedByStaffId",
                table: "ServiceJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobs_ProcessedByStaffId",
                table: "ServiceJobs",
                column: "ProcessedByStaffId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobs_Staff_ProcessedByStaffId",
                table: "ServiceJobs",
                column: "ProcessedByStaffId",
                principalTable: "Staff",
                principalColumn: "StaffId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobs_Staff_ProcessedByStaffId",
                table: "ServiceJobs");

            migrationBuilder.DropIndex(
                name: "IX_ServiceJobs_ProcessedByStaffId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "ProcessedByStaffId",
                table: "ServiceJobs");
        }
    }
}
