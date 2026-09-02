using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Teachers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoData",
                table: "Teachers",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "PhotoData",
                table: "Teachers");
        }
    }
}
