using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Linq;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class FixJsonUserKeyInfosCasting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
                ALTER TABLE ""UserDashboards"" 
                ALTER COLUMN ""JsonUserKeyInfos"" TYPE jsonb 
                USING ""JsonUserKeyInfos""::jsonb;
                ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
                ALTER TABLE ""UserDashboards"" 
                ALTER COLUMN ""JsonUserKeyInfos"" TYPE text 
                USING ""JsonUserKeyInfos""::text;
                ");
        }
    }
}
