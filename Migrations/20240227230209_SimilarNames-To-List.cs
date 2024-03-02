using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MealGeniusBackend.Migrations
{
    /// <inheritdoc />
    public partial class SimilarNamesToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""NotFoundGroceryItems"" 
              ALTER COLUMN ""SimilarNames"" TYPE text[] 
              USING string_to_array(""SimilarNames"", ',', '')::text[]"
            );

            migrationBuilder.Sql(
                @"ALTER TABLE ""GroceryItems"" 
              ALTER COLUMN ""SimilarNames"" TYPE text[] 
              USING string_to_array(""SimilarNames"", ',', '')::text[]"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""NotFoundGroceryItems"" 
              ALTER COLUMN ""SimilarNames"" TYPE text 
              USING array_to_string(""SimilarNames"", ',')"
            );

            migrationBuilder.Sql(
                @"ALTER TABLE ""GroceryItems"" 
              ALTER COLUMN ""SimilarNames"" TYPE text 
              USING array_to_string(""SimilarNames"", ',')"
            );
        }
    }

}
