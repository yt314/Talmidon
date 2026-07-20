using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGenderToStudentAndParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Students",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Parents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Parents");
        }
    }
}
