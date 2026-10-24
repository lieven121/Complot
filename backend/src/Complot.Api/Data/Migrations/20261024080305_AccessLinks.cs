using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccessLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessLinks_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccessLinkSite",
                columns: table => new
                {
                    LinkId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SiteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLinkSite", x => new { x.LinkId, x.SiteId });
                    table.ForeignKey(
                        name: "FK_AccessLinkSite_AccessLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "AccessLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessLinkSite_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLinks_PersonId",
                table: "AccessLinks",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLinks_TokenHash",
                table: "AccessLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessLinkSite_SiteId",
                table: "AccessLinkSite",
                column: "SiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessLinkSite");

            migrationBuilder.DropTable(
                name: "AccessLinks");
        }
    }
}
