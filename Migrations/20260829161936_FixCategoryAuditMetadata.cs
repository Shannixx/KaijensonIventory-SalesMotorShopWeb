using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class FixCategoryAuditMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CASE B: No reliable historical creator exists (Staff table was empty when categories were created).
            // Keep CreatedBy = NULL. Update CreatedAt to the date audit tracking was added (2026-08-29).
            migrationBuilder.Sql(@"
                UPDATE [Categories]
                SET [CreatedAt] = '2026-08-29T00:00:00Z'
                WHERE [CategoryId] IN (1, 2, 3);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to the arbitrary date used in EnhanceCategoryDetails migration
            migrationBuilder.Sql(@"
                UPDATE [Categories]
                SET [CreatedAt] = '2026-01-01T00:00:00Z'
                WHERE [CategoryId] IN (1, 2, 3);
            ");
        }
    }
}
