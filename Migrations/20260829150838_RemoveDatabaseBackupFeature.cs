using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDatabaseBackupFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupConfigurations");

            migrationBuilder.DropTable(
                name: "DatabaseBackups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BackupDirectory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Hour = table.Column<int>(type: "int", nullable: false),
                    LastAutomaticRun = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Minute = table.Column<int>(type: "int", nullable: false),
                    NextAutomaticRun = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetentionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseBackups",
                columns: table => new
                {
                    BackupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    BackupType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseBackups", x => x.BackupId);
                    table.ForeignKey(
                        name: "FK_DatabaseBackups_Staff_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Staff",
                        principalColumn: "StaffId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseBackups_CreatedBy",
                table: "DatabaseBackups",
                column: "CreatedBy");
        }
    }
}
