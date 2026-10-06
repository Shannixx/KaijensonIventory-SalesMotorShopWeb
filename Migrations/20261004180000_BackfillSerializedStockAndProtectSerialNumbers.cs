using KaijensonIventory_SalesMotorShopWeb.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004180000_BackfillSerializedStockAndProtectSerialNumbers")]
    public sealed class BackfillSerializedStockAndProtectSerialNumbers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing sold serials retain their numbers and sale links. Only the missing
            // physical on-hand units receive identities; rerunning cannot duplicate them.
            migrationBuilder.Sql(@"
;WITH Required AS
(
    SELECT p.ProductId,
           p.QuantityOnHand - (SELECT COUNT(*) FROM dbo.SerialUnits AS u
             WHERE u.ProductId = p.ProductId AND u.Status = 'Available'
               AND u.SalesTransactionId IS NULL) AS Missing
    FROM dbo.Products AS p
    WHERE p.IsSerialized = 1 AND p.QuantityOnHand > 0
), Numbers AS
(
    SELECT ProductId, 1 AS Number, Missing FROM Required WHERE Missing > 0
    UNION ALL
    SELECT ProductId, Number + 1, Missing FROM Numbers WHERE Number < Missing
)
INSERT INTO dbo.SerialUnits (SerialNumber, ProductId, Status, CreatedDate, SalesTransactionId, SoldDate)
SELECT 'SN-' + CASE WHEN ProductId < 1000000
                    THEN RIGHT('000000' + CONVERT(varchar(20), ProductId), 6)
                    ELSE CONVERT(varchar(20), ProductId) END
       + '-' + UPPER(REPLACE(CONVERT(varchar(36), NEWID()), '-', '')),
       ProductId, 'Available', SYSUTCDATETIME(), NULL, NULL
FROM Numbers
OPTION (MAXRECURSION 0);");

            // Guard the identity at the storage boundary, including updates outside EF.
            migrationBuilder.Sql(@"
EXEC(N'CREATE TRIGGER [dbo].[TR_SerialUnits_ImmutableSerialNumber]
ON [dbo].[SerialUnits] AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE([SerialNumber]) AND EXISTS (
        SELECT 1 FROM inserted AS i
        JOIN deleted AS d ON i.SerialUnitId = d.SerialUnitId
        WHERE i.SerialNumber COLLATE Latin1_General_100_BIN2
           <> d.SerialNumber COLLATE Latin1_General_100_BIN2
    )
        THROW 51001, ''A serial number cannot be changed after generation.'', 1;
END');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_SerialUnits_ImmutableSerialNumber];");
            // Do not delete generated serials: they can already be linked to sales.
        }
    }
}
