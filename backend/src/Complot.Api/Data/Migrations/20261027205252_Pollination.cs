using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Pollination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pollination_Group",
                table: "Varieties",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pollination_Need",
                table: "Varieties",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Pollination_RangeMeters",
                table: "Varieties",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pollination_Group",
                table: "Trees",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Pollination_Need",
                table: "Trees",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Pollination_RangeMeters",
                table: "Trees",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pollination_Group",
                table: "Varieties");

            migrationBuilder.DropColumn(
                name: "Pollination_Need",
                table: "Varieties");

            migrationBuilder.DropColumn(
                name: "Pollination_RangeMeters",
                table: "Varieties");

            migrationBuilder.DropColumn(
                name: "Pollination_Group",
                table: "Trees");

            migrationBuilder.DropColumn(
                name: "Pollination_Need",
                table: "Trees");

            migrationBuilder.DropColumn(
                name: "Pollination_RangeMeters",
                table: "Trees");
        }
    }
}
