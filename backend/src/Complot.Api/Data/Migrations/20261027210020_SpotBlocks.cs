using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpotBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpotColumns",
                table: "Plantings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SpotVertical",
                table: "Plantings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpotColumns",
                table: "Plantings");

            migrationBuilder.DropColumn(
                name: "SpotVertical",
                table: "Plantings");
        }
    }
}
