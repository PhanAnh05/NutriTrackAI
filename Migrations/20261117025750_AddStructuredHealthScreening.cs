using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutriTrackAI.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredHealthScreening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DoctorAwareOfPlan",
                table: "user_profiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "HealthScreeningAt",
                table: "user_profiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MedicalConditions",
                table: "user_profiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoctorAwareOfPlan",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "HealthScreeningAt",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "MedicalConditions",
                table: "user_profiles");
        }
    }
}
