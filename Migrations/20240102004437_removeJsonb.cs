using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Linq;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class removeJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonReponse",
                table: "UserDashboards");

            migrationBuilder.AlterColumn<string>(
                name: "JsonUserKeyInfos",
                table: "UserDashboards",
                type: "text",
                nullable: false,
                oldClrType: typeof(JObject),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<JObject>(
                name: "JsonUserKeyInfos",
                table: "UserDashboards",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "JsonReponse",
                table: "UserDashboards",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
