using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceJobAddOnsAndPriceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAddOn",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePriceSnapshot",
                table: "ServiceJobs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ServiceNameSnapshot",
                table: "ServiceJobs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "ServiceJobs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Freeze the catalog values currently referenced by legacy jobs. Payments
            // cannot reconstruct an earlier price, and no historical add-ons are inferred.
            migrationBuilder.Sql(@"
                UPDATE job
                SET job.ServiceNameSnapshot = service.ServiceName,
                    job.BasePriceSnapshot = service.ServicePrice,
                    job.TotalPrice = service.ServicePrice
                FROM ServiceJobs AS job
                INNER JOIN Services AS service ON service.ServiceId = job.ServiceId;");

            migrationBuilder.CreateTable(
                name: "ServiceJobAddOns",
                columns: table => new
                {
                    ServiceJobId = table.Column<int>(type: "int", nullable: false),
                    AddOnServiceId = table.Column<int>(type: "int", nullable: false),
                    AddOnNameSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceJobAddOns", x => new { x.ServiceJobId, x.AddOnServiceId });
                    table.ForeignKey(
                        name: "FK_ServiceJobAddOns_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "ServiceJobId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceJobAddOns_Services_AddOnServiceId",
                        column: x => x.AddOnServiceId,
                        principalTable: "Services",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceJobAddOns_AddOnServiceId",
                table: "ServiceJobAddOns",
                column: "AddOnServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceJobAddOns");

            migrationBuilder.DropColumn(
                name: "IsAddOn",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "BasePriceSnapshot",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "ServiceNameSnapshot",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "ServiceJobs");
        }
    }
}
