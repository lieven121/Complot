using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PiecesPerUnit",
                table: "VarietySuppliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SeedsPerGram",
                table: "Varieties",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PiecesPerUnit",
                table: "VarietySuppliers");

            migrationBuilder.DropColumn(
                name: "SeedsPerGram",
                table: "Varieties");
        }
    }
}
