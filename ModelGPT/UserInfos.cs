namespace MealGeniusBackend.ModelGPT
{
    public class UserInfos
    {
        public string CuisineType { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }
        public double Weight { get; set; }
        public double Height { get; set; }
        public string Objective { get; set; }
        public string Allergies { get; set; }
        public string CookingSkillLevel { get; set; }
        public List<string> PreferredIngredients { get; set; }
        public string DietaryPreferencesRestrictions { get; set; }
        public string HealthConditions { get; set; }
        public string FoodDislikes { get; set; }
        public string PreparationTime { get; set; }
        public int MealFrequency { get; set; }
        public int NumberOfPeople { get; set; }

        public string UserComments { get; set; }
    }
}