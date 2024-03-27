using System.Text;

namespace MealGeniusBackend.Models
{
    #region Models
    public class MealTotalBreakdown
    {
        public MealData MealDetails;
        public MealRecipeBreakdown MealInformation;
    }


    public class MealData
    {
        public string MealName { get; set; }
        public Macronutrients Macronutrients { get; set; }
        public Macronutrients_Pourcentage Macronutrients_Pourcentage { get; set; }
        public List<string> Micronutrients { get; set; }
        public string ServingSize { get; set; }
        public List<string> Ingredients { get; set; }
    }

    public class MealRecipeBreakdown
    {
        public string NameOfTheMeal { get; set; }
        public string Introduction { get; set; }
        public string Ingredients { get; set; }
        public string ServingText { get; set; }
        public string DetailedCookingInstructions { get; set; }
        public string AppliancesAndTools { get; set; }
        public string MealTimingRecommendations { get; set; }
        public string Macronutrients_Section { get; set; }
        public string Key_Micronutrients { get; set; }
        public string Health_Benefits { get; set; }
        public string Conclusion { get; set; }
    }
    public class Macronutrients
    {
        public string Proteins { get; set; }
        public string Carbohydrates { get; set; }
        public string Fats { get; set; }
        public string Calories { get; set; }

        public override string ToString()
        {
            return $"Proteins: {Proteins}, Carbohydrates: {Carbohydrates}, Fats: {Fats}, Calories: {Calories}";
        }
    }

    public class Macronutrients_Pourcentage
    {
        public string ProteinPercentage { get; set; }
        public string CarbsPercentage { get; set; }
        public string FatsPercentage { get; set; }
    }
    public class aMeal
    {
        public string MealName { get; set; }
        public List<string> MealType { get; set; }
        public string PreparationSkill { get; set; }
        public List<string> MoodSuitability { get; set; }
        public List<string> Tags { get; set; }

        public Macronutrients Macronutrients { get; set; }

        public Macronutrients_Pourcentage Macronutrients_Pourcentage { get; set; }
        public List<string> Micronutrients { get; set; }
        public string ServingSize { get; set; }
        public List<string> Ingredients { get; set; }


        public string Recipe { get; set; }  // <-- New property

        public MealRecipeBreakdown mealRecipeBreakdown { get; set; }
        public string MealImage { get; set; }

        public string MealBackgroundColor { get; set; }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"Meal Name: {MealName}");

            if (MealType != null && MealType.Any())
                stringBuilder.AppendLine($"Meal Type: {string.Join(", ", MealType)}");

            if (PreparationSkill != null && PreparationSkill.Any())
                stringBuilder.AppendLine($"Preparation Skill: {string.Join(", ", PreparationSkill)}");

            if (MoodSuitability != null && MoodSuitability.Any())
                stringBuilder.AppendLine($"Mood Suitability: {string.Join(", ", MoodSuitability)}");

            if (Macronutrients != null)
                stringBuilder.AppendLine($"Macronutrients: {Macronutrients}");

            if (Micronutrients != null && Micronutrients.Any())
                stringBuilder.AppendLine($"Micronutrients: {string.Join(", ", Micronutrients)}");

            if (!string.IsNullOrWhiteSpace(ServingSize))
                stringBuilder.AppendLine($"Serving Size: {ServingSize}");

            if (Ingredients != null && Ingredients.Any())
                stringBuilder.AppendLine($"Ingredients: {string.Join(", ", Ingredients)}");

            return stringBuilder.ToString();
        }
    }

    public class UserMealsRoot
    {
        public List<aMeal> UserMeals { get; set; }
        public List<string> GetAllIngredients()
        {
            List<string> allIngredients = new List<string>();

            foreach (aMeal meal in UserMeals)
            {
                allIngredients.AddRange(meal.Ingredients);
            }

            return allIngredients;
        }

        public string DisplayAllIngredients()
        {
            List<string> allIngredients = GetAllIngredients();
            var stringBuilder = new StringBuilder();

            foreach (string ingredient in allIngredients)
            {
                stringBuilder.AppendLine($"- {ingredient}");
            }

            return stringBuilder.ToString();
        }
    }
    #endregion

}
