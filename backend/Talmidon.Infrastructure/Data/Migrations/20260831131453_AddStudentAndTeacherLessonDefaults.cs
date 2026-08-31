using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentAndTeacherLessonDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultDurationMinutes",
                table: "Teachers",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "DefaultDurationMinutes",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultPricePerLesson",
                table: "Students",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Teachers_DefaultDurationMinutes_Positive",
                table: "Teachers",
                sql: "\"DefaultDurationMinutes\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_DefaultDurationMinutes_Positive",
                table: "Students",
                sql: "\"DefaultDurationMinutes\" IS NULL OR \"DefaultDurationMinutes\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Students_DefaultPricePerLesson_NonNegative",
                table: "Students",
                sql: "\"DefaultPricePerLesson\" IS NULL OR \"DefaultPricePerLesson\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Teachers_DefaultDurationMinutes_Positive",
                table: "Teachers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_DefaultDurationMinutes_Positive",
                table: "Students");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Students_DefaultPricePerLesson_NonNegative",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DefaultDurationMinutes",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "DefaultDurationMinutes",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DefaultPricePerLesson",
                table: "Students");
        }
    }
}
