using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class SyncDeliveryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF COL_LENGTH('dbo.Deliveries','ReceivedBy') IS NULL ALTER TABLE dbo.Deliveries ADD ReceivedBy int NULL; IF COL_LENGTH('dbo.Deliveries','Remarks') IS NULL ALTER TABLE dbo.Deliveries ADD Remarks nvarchar(500) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
