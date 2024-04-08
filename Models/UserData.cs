namespace MealGeniusBackend.Models
{
    using System.Collections.Generic;

    public class UserData
    {
        public UserDetails Details { get; set; }
        public UserNutritionalGoals NutritionalGoals { get; set; }
        public UserMealsAndGroceryPreferences MealsAndGroceries { get; set; }
    }

    public class UserDetails
    {
        public string Name { get; set; }
        public string Age { get; set; }
        public string SexOrGender { get; set; }
        public double Weight { get; set; }
        public double Height { get; set; }
        public string WeightUnit { get; set; }
        public string HeightUnit { get; set; }
        public string ActivityLevel { get; set; }
    }

    public class UserNutritionalGoals
    {
        public List<string> Goals { get; set; }
        public double TargetWeight { get; set; }
        public string PaceOfWeightChange { get; set; }
        public List<string> DietaryRestrictionsBasedOnFoodSource { get; set; }

        public List<string> DietaryRestrictionsBasedOnMacroNutritients { get; set; }

        public List<string> Allergies { get; set; }
        public List<string> HealthConditions { get; set; }
        public List<string> MentalHealthConditions { get; set; }
    }

    public class UserMealsAndGroceryPreferences
    {
        public List<string> CuisineLikes { get; set; }
        public List<string> FavoriteMealTypes { get; set; }

        public List<string> EssentialIngredients { get; set; }
        public List<string> DislikedIngredients { get; set; }
        public string PreferredCookingTime { get; set; }
        public string MealPreparationStyle { get; set; }
        public string MeasurementForLiquids { get; set; }
        public string MeasurementForTemperature { get; set; }

        public string MeasurementForWeightUnit { get; set; }

    }



}