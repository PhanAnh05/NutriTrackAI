using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriTrackAI.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveCaloriePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaloriePlanJson",
                table: "goals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaloriePlanJson",
                table: "goals");
        }
    }
}
