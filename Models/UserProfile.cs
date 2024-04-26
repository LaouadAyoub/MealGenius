using Newtonsoft.Json;

namespace MealGeniusBackend.Models
{
    public class UserProfile
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("age")]
        public string Age { get; set; }
        [JsonProperty("sexe")]
        public string Sexe { get; set; }

        [JsonProperty("otherSexeDetails")]
        public string? OtherSexeDetails { get; set; }

        [JsonProperty("activityLevel")]
        public string ActivityLevel { get; set; }


        [JsonProperty("weight")]
        public string Weight { get; set; }
        [JsonProperty("height")]
        public string Height { get; set; }
        [JsonProperty("weightUnit")]
        public string WeightUnit { get; set; }
        [JsonProperty("heightUnit")]
        public string HeightUnit { get; set; }




        [JsonProperty("mainGoals")]
        public List<string>? MainGoals { get; set; }
        [JsonProperty("otherMainGoals")]
        public string? OtherMainGoals { get; set; }
        [JsonProperty("weightGoal")]
        public string? WeightGoal { get; set; }

        [JsonProperty("weightGoalUnit")]
        public string? WeightGoalUnit { get; set; }
        [JsonProperty("pace")]
        public string? Pace { get; set; }

        [JsonProperty("dietaryPreferences")]
        public List<string>? DietaryPreferences { get; set; }
        [JsonProperty("otherDietaryPreferences")]
        public string? OtherDietaryPreferences { get; set; }

        [JsonProperty("dietaryPreferencesMacros")]
        public List<string>? DietaryPreferencesMacros { get; set; }

        [JsonProperty("otherDietaryPreferencesMacros")]
        public string? OtherDietaryPreferencesMacros { get; set; }


        [JsonProperty("FoodAllergies")]
        public List<string>? FoodAllergies { get; set; }

        [JsonProperty("otherFoodAllergies")]
        public string? OtherFoodAllergies { get; set; }


        [JsonProperty("healthConditions")]  
        public List<string>? HealthConditions { get; set; }

        [JsonProperty("otherHealthConditions")]
        public string? OtherHealthConditions { get; set; }

        [JsonProperty("mentalHealthConditions")]
        public List<string>? MentalHealthConditions { get; set; }
        [JsonProperty("otherMentalHealthConditions")]
        public string? OtherMentalHealthConditions { get; set; }




        [JsonProperty("cuisinePreferences")]
        public List<string>? CuisinePreferences { get; set; }
        [JsonProperty("otherCuisinePreferences")]
        public string? OtherCuisinePreferences { get; set; }
        [JsonProperty("favoriteDishes")]
        public List<FavoriteDish>? FavoriteDishes { get; set; }

        [JsonProperty("mustHaveIngredients")]
        public List<Ingredient>? MustHaveIngredients { get; set; }
        [JsonProperty("dislikedIngredients")]
        public List<Ingredient>? UnlikedIngredients { get; set; }
        [JsonProperty("idealCookingTime")]
        public string? IdealCookingTime { get; set; }

        [JsonProperty("servingPreference")]
        public string? ServingPreference { get; set; }

        [JsonProperty("liquidUnit")]
        public string? LiquidUnit { get; set; }

        [JsonProperty("temperatureUnit")]
        public string? TemperatureUnit { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }

    public class FavoriteDish
    {
        [JsonProperty("id")]
        public string? Id     { get; set; }
        [JsonProperty("text")]
        public string? Text { get; set; }
    }

    public class Ingredient
    {
        [JsonProperty("id")]
        public string? Id { get; set; }
        [JsonProperty("text")]
        public string? Text { get; set; }
    }
}
