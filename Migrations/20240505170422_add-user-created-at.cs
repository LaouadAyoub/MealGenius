using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class addusercreatedat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UserCreatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserCreatedAt",
                table: "AspNetUsers");
        }
    }
}
