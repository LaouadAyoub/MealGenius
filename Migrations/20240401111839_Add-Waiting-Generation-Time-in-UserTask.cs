using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitingGenerationTimeinUserTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DashboardGenerationEstimationTime",
                table: "Tasks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "DashboardsGenerationExcecutedAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GroceryListGenerationEstimationTime",
                table: "Tasks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "GroceryListsGenerationExcecutedAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MealsGenerationEstimationTime",
                table: "Tasks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "MealsGenerationExcecutedAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MealsImagesGenerationExcecutedAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DashboardGenerationEstimationTime",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "DashboardsGenerationExcecutedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GroceryListGenerationEstimationTime",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GroceryListsGenerationExcecutedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "MealsGenerationEstimationTime",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "MealsGenerationExcecutedAt",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "MealsImagesGenerationExcecutedAt",
                table: "Tasks");
        }
    }
}
