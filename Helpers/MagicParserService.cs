using System.Text.RegularExpressions;
using MealGeniusBackend.Models;
namespace MealGeniusBackend.Helpers
{
    public static class MagicParserService
    {
        public static MealRecipeBreakdown ParseRecipe(string recipeText)
        {
            var breakdown = new MealRecipeBreakdown();

            // Regex pattern to match sections
            var pattern = @"##\s*(?<sectionName>[^#]+)\n(?<sectionText>(.|\n)+?)(?=\n##|$)";

            var matches = Regex.Matches(recipeText, pattern, RegexOptions.Singleline);

            bool isFoundServingSize = false; // Initialize the flag to track if "Serving Size" has been found

            foreach (Match match in matches)
            {
                var sectionName = match.Groups["sectionName"].Value.Trim();
                var sectionText = match.Groups["sectionText"].Value.Trim();

                // Check if the current section is "Serving Size"
                if (sectionName.Equals("Serving Size", StringComparison.OrdinalIgnoreCase))
                {
                    breakdown.ServingText = sectionText;
                    isFoundServingSize = true;
                }

                switch (sectionName)
                {
                    case "Introduction":
                        breakdown.Introduction = sectionText;
                        break;
                    case "Ingredients":
                        breakdown.Ingredients = sectionText;
                        // If "Serving Size" hasn't been found in its own section, look for it within "Ingredients"
                        if (!isFoundServingSize)
                        {
                            var servingSizeMatch = Regex.Match(sectionText, @"\*\*Serving Size:\*\* [^\n]+", RegexOptions.Singleline);
                            if (servingSizeMatch.Success)
                            {
                                breakdown.ServingText = servingSizeMatch.Value;
                                isFoundServingSize = true;
                            }
                        }
                        break;
                    // Handle other sections as needed
                    case "Detailed Cooking Instructions":
                        breakdown.DetailedCookingInstructions = sectionText;
                        break;
                    case "Appliances and Tools":
                        breakdown.AppliancesAndTools = sectionText;
                        break;
                    case "Meal Timing Recommendations":
                        breakdown.MealTimingRecommendations = sectionText;
                        break;
                    case "Macronutrients Breakdown":
                        breakdown.Macronutrients_Section = sectionText;
                        break;
                    case "Key Micronutrients":
                        breakdown.Key_Micronutrients = sectionText;
                        break;
                    case "Health Benefits":
                        breakdown.Health_Benefits = sectionText;
                        break;
                    case "Conclusion":
                        breakdown.Conclusion = sectionText;
                        break;
                }
            }

            breakdown.NameOfTheMeal = ""; // Set the name of the meal here

            return breakdown;
        }
    }
}
