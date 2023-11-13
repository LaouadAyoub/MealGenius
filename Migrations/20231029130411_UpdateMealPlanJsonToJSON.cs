using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMealPlanJsonToJSON : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"MealPlans\" ALTER COLUMN \"MealPlanJson\" TYPE jsonb USING \"MealPlanJson\"::jsonb;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"MealPlans\" ALTER COLUMN \"MealPlanJson\" TYPE text USING \"MealPlanJson\"::text;");
        }
    }
}
