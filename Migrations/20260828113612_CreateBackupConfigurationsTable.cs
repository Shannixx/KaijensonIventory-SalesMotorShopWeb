using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class CreateBackupConfigurationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Hour = table.Column<int>(type: "int", nullable: false, defaultValue: 21),
                    Minute = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    RetentionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    BackupDirectory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastAutomaticRun = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAutomaticRun = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupConfigurations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BackupConfigurations");
        }
    }
}
