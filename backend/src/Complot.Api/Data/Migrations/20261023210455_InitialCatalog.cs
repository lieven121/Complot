using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Families",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LatinName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NameNl = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RotationRestYears = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Families", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsOwnSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    DefaultFamilyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RotationExempt = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Families_DefaultFamilyId",
                        column: x => x.DefaultFamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoryCompanions",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OtherCategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Relation = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryCompanions", x => new { x.CategoryId, x.OtherCategoryId });
                    table.ForeignKey(
                        name: "FK_CategoryCompanions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryCompanions_Categories_OtherCategoryId",
                        column: x => x.OtherCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Varieties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FamilyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Cultivar = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    LatinName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Life = table.Column<int>(type: "INTEGER", nullable: false),
                    SowDepthCm = table.Column<decimal>(type: "TEXT", nullable: true),
                    SpacingInRowCm = table.Column<int>(type: "INTEGER", nullable: true),
                    SpacingBetweenRowsCm = table.Column<int>(type: "INTEGER", nullable: true),
                    HeightCm = table.Column<int>(type: "INTEGER", nullable: true),
                    Sun = table.Column<string>(type: "TEXT", nullable: false),
                    WeeksIndoor = table.Column<int>(type: "INTEGER", nullable: true),
                    DaysToFirstHarvest = table.Column<int>(type: "INTEGER", nullable: true),
                    HarvestWeeks = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxYearsInBed = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Germination_Max = table.Column<int>(type: "INTEGER", nullable: true),
                    Germination_Min = table.Column<int>(type: "INTEGER", nullable: true),
                    Harvest_From = table.Column<int>(type: "INTEGER", nullable: false),
                    Harvest_To = table.Column<int>(type: "INTEGER", nullable: false),
                    PlantOut_From = table.Column<int>(type: "INTEGER", nullable: true),
                    PlantOut_To = table.Column<int>(type: "INTEGER", nullable: true),
                    SowIndoor_From = table.Column<int>(type: "INTEGER", nullable: true),
                    SowIndoor_To = table.Column<int>(type: "INTEGER", nullable: true),
                    SowOutdoor_From = table.Column<int>(type: "INTEGER", nullable: true),
                    SowOutdoor_To = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Actions = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Varieties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Varieties_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Varieties_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VarietyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    BoughtOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BestBefore = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Varieties_VarietyId",
                        column: x => x.VarietyId,
                        principalTable: "Varieties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VarietySuppliers",
                columns: table => new
                {
                    VarietyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VarietySuppliers", x => new { x.VarietyId, x.SupplierId });
                    table.ForeignKey(
                        name: "FK_VarietySuppliers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VarietySuppliers_Varieties_VarietyId",
                        column: x => x.VarietyId,
                        principalTable: "Varieties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DefaultFamilyId",
                table: "Categories",
                column: "DefaultFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryCompanions_OtherCategoryId",
                table: "CategoryCompanions",
                column: "OtherCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Families_LatinName",
                table: "Families",
                column: "LatinName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_SupplierId",
                table: "InventoryLots",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_VarietyId",
                table: "InventoryLots",
                column: "VarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Varieties_CategoryId",
                table: "Varieties",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Varieties_FamilyId",
                table: "Varieties",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_Varieties_Name",
                table: "Varieties",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_VarietySuppliers_SupplierId",
                table: "VarietySuppliers",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryCompanions");

            migrationBuilder.DropTable(
                name: "InventoryLots");

            migrationBuilder.DropTable(
                name: "VarietySuppliers");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Varieties");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Families");
        }
    }
}
