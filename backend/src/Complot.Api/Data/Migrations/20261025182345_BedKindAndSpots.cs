using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BedKindAndSpots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Count",
                table: "Plantings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpotX",
                table: "Plantings",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpotY",
                table: "Plantings",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Beds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Count",
                table: "Plantings");

            migrationBuilder.DropColumn(
                name: "SpotX",
                table: "Plantings");

            migrationBuilder.DropColumn(
                name: "SpotY",
                table: "Plantings");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Beds");
        }
    }
}
