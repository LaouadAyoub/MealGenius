using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class NotFoundGroceryItems_SimilarNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "SimilarNames",
                table: "NotFoundGroceryItems",
                type: "text[]",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SimilarNames",
                table: "NotFoundGroceryItems");
        }
    }
}
