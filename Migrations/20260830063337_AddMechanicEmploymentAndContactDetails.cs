using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaijensonIventory_SalesMotorShopWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicEmploymentAndContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateHired",
                table: "Mechanics",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "Mechanics",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HiredBy",
                table: "Mechanics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Mechanics",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkStatus",
                table: "Mechanics",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "Mechanics",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mechanics_HiredBy",
                table: "Mechanics",
                column: "HiredBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Mechanics_Staff_HiredBy",
                table: "Mechanics",
                column: "HiredBy",
                principalTable: "Staff",
                principalColumn: "StaffId",
                onDelete: ReferentialAction.SetNull);
        // Set default employment data for existing mechanics
        migrationBuilder.Sql(@"
DECLARE @AdminId int = (SELECT TOP 1 StaffId FROM Staff WHERE Role = 'Admin' ORDER BY StaffId);
UPDATE Mechanics
SET Status = 'Active',
    WorkStatus = 'Available',
    DateHired = '2026-08-30',
    HiredBy = @AdminId
WHERE Status = '' OR Status IS NULL OR WorkStatus = '' OR WorkStatus IS NULL;
");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mechanics_Staff_HiredBy",
                table: "Mechanics");

            migrationBuilder.DropIndex(
                name: "IX_Mechanics_HiredBy",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "DateHired",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "HiredBy",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "WorkStatus",
                table: "Mechanics");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "Mechanics");
        }
    }
}
