namespace MealGeniusBackend.Models
{
    public class aMealWithoutRecipe
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

        public MealRecipeBreakdown mealRecipeBreakdown { get; set; }
        public string MealImage { get; set; }

        public string MealBackgroundColor { get; set; }
    }



}
