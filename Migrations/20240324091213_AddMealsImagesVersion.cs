using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMealsImagesVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MealsImagesVersion",
                table: "MealPlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealsImagesVersion",
                table: "MealPlans");
        }
    }
}
