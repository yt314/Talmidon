using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Talmidon.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherDurationPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PricePer30Minutes",
                table: "Teachers",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePer45Minutes",
                table: "Teachers",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Teachers_PricePer30Minutes_NonNegative",
                table: "Teachers",
                sql: "\"PricePer30Minutes\" IS NULL OR \"PricePer30Minutes\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Teachers_PricePer45Minutes_NonNegative",
                table: "Teachers",
                sql: "\"PricePer45Minutes\" IS NULL OR \"PricePer45Minutes\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Teachers_PricePer30Minutes_NonNegative",
                table: "Teachers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Teachers_PricePer45Minutes_NonNegative",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "PricePer30Minutes",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "PricePer45Minutes",
                table: "Teachers");
        }
    }
}
