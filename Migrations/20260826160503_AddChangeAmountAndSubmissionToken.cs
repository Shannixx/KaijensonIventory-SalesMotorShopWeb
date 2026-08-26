using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeAmountAndSubmissionToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SerialUnits_SalesTransactions_SalesTransactionId",
                table: "SerialUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobs_Staff_ProcessedByStaffId",
                table: "ServiceJobs");

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                table: "ServiceJobs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                            name: "SubmissionToken",
                            table: "ServiceJobs",
                            type: "nvarchar(64)",
                            maxLength: 64,
                            nullable: false,
                            defaultValueSql: "NEWID()");

                        migrationBuilder.CreateIndex(
                            name: "IX_ServiceJobs_SubmissionToken",
                            table: "ServiceJobs",
                            column: "SubmissionToken",
                            unique: true);


            migrationBuilder.AddForeignKey(
                name: "FK_SerialUnits_SalesTransactions_SalesTransactionId",
                table: "SerialUnits",
                column: "SalesTransactionId",
                principalTable: "SalesTransactions",
                principalColumn: "TransactionId");

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
                name: "FK_SerialUnits_SalesTransactions_SalesTransactionId",
                table: "SerialUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceJobs_Staff_ProcessedByStaffId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "ChangeAmount",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "SubmissionToken",
                table: "ServiceJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_SerialUnits_SalesTransactions_SalesTransactionId",
                table: "SerialUnits",
                column: "SalesTransactionId",
                principalTable: "SalesTransactions",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceJobs_Staff_ProcessedByStaffId",
                table: "ServiceJobs",
                column: "ProcessedByStaffId",
                principalTable: "Staff",
                principalColumn: "StaffId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
