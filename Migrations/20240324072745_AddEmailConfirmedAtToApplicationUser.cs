using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmedAtToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UserDashboardVersion",
                table: "UserDashboards",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "GroceryListVersion",
                table: "MealPlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MealPlanVersion",
                table: "MealPlans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserDashboardVersion",
                table: "UserDashboards");

            migrationBuilder.DropColumn(
                name: "GroceryListVersion",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "MealPlanVersion",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "EmailConfirmedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AspNetUsers");
        }
    }
}
