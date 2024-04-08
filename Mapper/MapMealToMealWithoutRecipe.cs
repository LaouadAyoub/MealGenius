using MealGeniusBackend.Models;

namespace MealGeniusBackend.Mapper
{
    public static class MealMapper
    {

        public static aMealWithoutRecipe MapMealToMealWithoutRecipe(aMeal meal)
        {
            return new aMealWithoutRecipe
            {
                MealName = meal.MealName,
                MealType = meal.MealType,
                PreparationSkill = meal.PreparationSkill,
                MoodSuitability = meal.MoodSuitability,
                Tags = meal.Tags,
                Macronutrients = meal.Macronutrients,
                Macronutrients_Pourcentage = meal.Macronutrients_Pourcentage,
                Micronutrients = meal.Micronutrients,
                ServingSize = meal.ServingSize,
                Ingredients = meal.Ingredients,
                mealRecipeBreakdown = meal.mealRecipeBreakdown,
                MealImage = meal.MealImage,
                MealBackgroundColor = meal.MealBackgroundColor
            };
        }
    }
}
