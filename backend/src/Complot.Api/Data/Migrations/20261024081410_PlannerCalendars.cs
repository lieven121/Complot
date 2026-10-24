using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Complot.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlannerCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskStates_AssigneeId",
                table: "TaskStates");

            migrationBuilder.AddColumn<string>(
                name: "CalendarError",
                table: "TaskStates",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarEventId",
                table: "TaskStates",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CalendarPersonId",
                table: "TaskStates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarTitle",
                table: "TaskStates",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalendarFeedToken",
                table: "People",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalendarLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarLinks_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskStates_AssigneeId_ScheduledStart",
                table: "TaskStates",
                columns: new[] { "AssigneeId", "ScheduledStart" });

            migrationBuilder.CreateIndex(
                name: "IX_People_CalendarFeedToken",
                table: "People",
                column: "CalendarFeedToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarLinks_PersonId",
                table: "CalendarLinks",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarLinks");

            migrationBuilder.DropIndex(
                name: "IX_TaskStates_AssigneeId_ScheduledStart",
                table: "TaskStates");

            migrationBuilder.DropIndex(
                name: "IX_People_CalendarFeedToken",
                table: "People");

            migrationBuilder.DropColumn(
                name: "CalendarError",
                table: "TaskStates");

            migrationBuilder.DropColumn(
                name: "CalendarEventId",
                table: "TaskStates");

            migrationBuilder.DropColumn(
                name: "CalendarPersonId",
                table: "TaskStates");

            migrationBuilder.DropColumn(
                name: "CalendarTitle",
                table: "TaskStates");

            migrationBuilder.DropColumn(
                name: "CalendarFeedToken",
                table: "People");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStates_AssigneeId",
                table: "TaskStates",
                column: "AssigneeId");
        }
    }
}
