using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PolylinesAndTreeVarieties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Points",
                table: "Paths",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Points",
                table: "Markers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CrownDepth",
                table: "Trees",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            // Existing trees are round (ShapeKind.Ellipse).
            migrationBuilder.AddColumn<int>(
                name: "Outline",
                table: "Trees",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "VarietyId",
                table: "Trees",
                type: "TEXT",
                nullable: true);

            // A rectangular path becomes a line along its long side, as wide as its short side.
            migrationBuilder.Sql("""
                UPDATE Paths SET
                    Points = CASE WHEN Shape_Width >= Shape_Height
                        THEN json_array(json_object('X', Shape_X, 'Y', Shape_Y + Shape_Height / 2), json_object('X', Shape_X + Shape_Width, 'Y', Shape_Y + Shape_Height / 2))
                        ELSE json_array(json_object('X', Shape_X + Shape_Width / 2, 'Y', Shape_Y), json_object('X', Shape_X + Shape_Width / 2, 'Y', Shape_Y + Shape_Height))
                    END,
                    Shape_Width = MIN(Shape_Width, Shape_Height);
                """);
            // A line marker (MarkerKind.Line) from (X, Y) by (Width, Height) becomes a line through its two ends.
            migrationBuilder.Sql("""
                UPDATE Markers SET Points = '[]' WHERE Kind <> 1;
                UPDATE Markers SET
                    Points = json_array(json_object('X', X, 'Y', Y), json_object('X', X + Width, 'Y', Y + Height)),
                    X = 0, Y = 0, Width = 0, Height = 0
                WHERE Kind = 1;
                """);

            migrationBuilder.RenameColumn(
                name: "CrownDiameter",
                table: "Trees",
                newName: "CrownWidth");

            migrationBuilder.Sql("UPDATE Trees SET CrownDepth = CrownWidth;");

            migrationBuilder.RenameColumn(
                name: "Shape_Width",
                table: "Paths",
                newName: "Width");

            migrationBuilder.DropColumn(
                name: "Shape_Height",
                table: "Paths");

            migrationBuilder.DropColumn(
                name: "Shape_Kind",
                table: "Paths");

            migrationBuilder.DropColumn(
                name: "Shape_X",
                table: "Paths");

            migrationBuilder.DropColumn(
                name: "Shape_Y",
                table: "Paths");

            migrationBuilder.CreateIndex(
                name: "IX_Trees_VarietyId",
                table: "Trees",
                column: "VarietyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trees_Varieties_VarietyId",
                table: "Trees",
                column: "VarietyId",
                principalTable: "Varieties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trees_Varieties_VarietyId",
                table: "Trees");

            migrationBuilder.DropIndex(
                name: "IX_Trees_VarietyId",
                table: "Trees");

            migrationBuilder.DropColumn(
                name: "CrownDepth",
                table: "Trees");

            migrationBuilder.DropColumn(
                name: "Outline",
                table: "Trees");

            migrationBuilder.DropColumn(
                name: "VarietyId",
                table: "Trees");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Paths");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "Markers");

            migrationBuilder.RenameColumn(
                name: "CrownWidth",
                table: "Trees",
                newName: "CrownDiameter");

            migrationBuilder.RenameColumn(
                name: "Width",
                table: "Paths",
                newName: "Shape_Width");

            migrationBuilder.AddColumn<double>(
                name: "Shape_Height",
                table: "Paths",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Shape_Kind",
                table: "Paths",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Shape_X",
                table: "Paths",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Shape_Y",
                table: "Paths",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
