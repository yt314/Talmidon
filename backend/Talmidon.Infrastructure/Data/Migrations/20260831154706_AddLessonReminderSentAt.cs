using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonReminderSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                table: "Lessons",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Lessons");
        }
    }
}
