using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptingStudentsHolidaySkipAndSiteFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptingStudents",
                table: "Teachers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkipJewishHolidays",
                table: "LessonSeries",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "SiteFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ContactInfo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsHandled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteFeedback", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteFeedback_IsHandled_CreatedAt",
                table: "SiteFeedback",
                columns: new[] { "IsHandled", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteFeedback");

            migrationBuilder.DropColumn(
                name: "SkipJewishHolidays",
                table: "LessonSeries");

            migrationBuilder.DropColumn(
                name: "AcceptingStudents",
                table: "Teachers");
        }
    }
}
