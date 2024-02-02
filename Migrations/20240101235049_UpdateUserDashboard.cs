using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserDetails",
                table: "UserDashboards",
                newName: "WaterIntake");

            migrationBuilder.RenameColumn(
                name: "SummarySection",
                table: "UserDashboards",
                newName: "MicroGuide");

            migrationBuilder.RenameColumn(
                name: "CaloricNeedsInitialContent",
                table: "UserDashboards",
                newName: "MacroTargets");

            migrationBuilder.RenameColumn(
                name: "CaloricNeedsExpandedText",
                table: "UserDashboards",
                newName: "JsonUserKeyInfos");

            migrationBuilder.RenameColumn(
                name: "BmrInitialContent",
                table: "UserDashboards",
                newName: "JsonReponse");

            migrationBuilder.RenameColumn(
                name: "BmrExpandedText",
                table: "UserDashboards",
                newName: "GeneralNutritionGuide");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WaterIntake",
                table: "UserDashboards",
                newName: "UserDetails");

            migrationBuilder.RenameColumn(
                name: "MicroGuide",
                table: "UserDashboards",
                newName: "SummarySection");

            migrationBuilder.RenameColumn(
                name: "MacroTargets",
                table: "UserDashboards",
                newName: "CaloricNeedsInitialContent");

            migrationBuilder.RenameColumn(
                name: "JsonUserKeyInfos",
                table: "UserDashboards",
                newName: "CaloricNeedsExpandedText");

            migrationBuilder.RenameColumn(
                name: "JsonReponse",
                table: "UserDashboards",
                newName: "BmrInitialContent");

            migrationBuilder.RenameColumn(
                name: "GeneralNutritionGuide",
                table: "UserDashboards",
                newName: "BmrExpandedText");
        }
    }
}
