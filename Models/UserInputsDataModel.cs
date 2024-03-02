namespace MealGeniusBackend.Models
{
    using System.Collections.Generic;


    public class UserDetailsDtoLite
    {
        public string Name { get; set; }
        public string Age { get; set; }
        public string Sex { get; set; }
        public string GenderIdentity { get; set; }
        public string Weight { get; set; }
        public string Height { get; set; }
        public string ActivityLevel { get; set; }
    }
    public class UserDetailsDto
    {
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }

        public string Name { get; set; }
        public string Age { get; set; }
        public string Sex { get; set; }
        public string GenderIdentity { get; set; }
        public string Weight { get; set; }
        public string Height { get; set; }
        public string ActivityLevel { get; set; }
    }

    public class HealthAndNutritionPreferencesDto
    {
        public List<string> NutritionalGoals { get; set; }
        public string NutritionKnowledgeLevel { get; set; }
        public string WeightManagementSpecifics { get; set; }
        public string GoalWeight { get; set; }
        public string WeightPacePreference { get; set; }
        public DietaryPreferencesDto DietaryPreferences { get; set; }
        public List<string> FoodAllergies { get; set; }
        public List<string> ManagingHealthConditions { get; set; }
        public List<string> ManagingMentalHealthConditions { get; set; }
        public string AdditionalNutritionalInformation { get; set; }
    }

    public class DietaryPreferencesDto
    {
        public List<string> FoodSource { get; set; }
        public List<string> MacronutrientFocus { get; set; }
    }

    public class MealPlanPreferencesDto
    {
        public string MealSizePreference { get; set; }
        public List<string> FavoriteCuisines { get; set; }
        public List<string> FavoriteDishes { get; set; }
        public List<string> FavoriteDishCategories { get; set; }
        public string CookingSkillLevel { get; set; }
        public GroceryListPreferencesDto GroceryListPreferences { get; set; }
        public string SnackingHabits { get; set; }
        public List<string> SupplementUse { get; set; }
        public string BudgetConstraints { get; set; }
    }

    public class GroceryListPreferencesDto
    {
        public string ShoppingStyle { get; set; }
        public List<string> StapleItems { get; set; }
        public List<string> DislikedIngredients { get; set; }
        public string IdealCookingPeriodOfTime { get; set; }
        public List<string> KitchenAppliancesAvailable { get; set; }
    }

    public class UserInputsDataModel
    {
        public UserDetailsDto UserDetails { get; set; }
        public HealthAndNutritionPreferencesDto HealthAndNutritionPreferences { get; set; }
        public MealPlanPreferencesDto MealPlanPreferences { get; set; }
    }    
    
    public class UserInputsDataModelLite
    {
        public UserDetailsDtoLite UserDetails { get; set; }
        public HealthAndNutritionPreferencesDto HealthAndNutritionPreferences { get; set; }
        public MealPlanPreferencesDto MealPlanPreferences { get; set; }
    }



}