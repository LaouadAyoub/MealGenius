using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddcolumnCompressedImageUrltoGroceryItemTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompressedImageUrl",
                table: "GroceryItems",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompressedImageUrl",
                table: "GroceryItems");
        }
    }
}
