using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "Lessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LessonSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartTimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    SeriesStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: true),
                    OccurrencesGenerated = table.Column<int>(type: "integer", nullable: false),
                    LastGeneratedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSeries", x => x.Id);
                    table.UniqueConstraint("AK_LessonSeries_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.CheckConstraint("CK_LessonSeries_EndCondition", "\"EndDate\" IS NULL OR \"OccurrenceCount\" IS NULL");
                    table.ForeignKey(
                        name: "FK_LessonSeries_Students_StudentId_TenantId",
                        columns: x => new { x.StudentId, x.TenantId },
                        principalTable: "Students",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonSeries_Teachers_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_SeriesId",
                table: "Lessons",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_StudentId_TenantId",
                table: "LessonSeries",
                columns: new[] { "StudentId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSeries_TenantId_IsActive",
                table: "LessonSeries",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_LessonSeries_SeriesId",
                table: "Lessons",
                column: "SeriesId",
                principalTable: "LessonSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_LessonSeries_SeriesId",
                table: "Lessons");

            migrationBuilder.DropTable(
                name: "LessonSeries");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_SeriesId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Lessons");
        }
    }
}
