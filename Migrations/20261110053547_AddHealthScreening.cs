using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriTrackAI.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HealthScreeningJson",
                table: "user_profiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthScreeningJson",
                table: "user_profiles");
        }
    }
}
