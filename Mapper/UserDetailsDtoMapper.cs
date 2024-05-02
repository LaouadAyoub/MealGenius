using MealGeniusBackend.Models;
using System.Collections.Generic;
using System.Linq;

namespace MealGeniusBackend.Mapper
{
    public static class DtoMapper
    {
        public static UserData MapUserProfileToUserData(UserProfile userProfile)
        {
            var userData = new UserData
            {
                Details = new UserDetails
                {
                    Name = userProfile.Name,
                    Age = userProfile.Age,
                    SexOrGender = (userProfile.Sexe == "Other") ? userProfile.OtherSexeDetails : userProfile.Sexe,
                    Weight = userProfile.Weight + " " + userProfile.WeightUnit,
                    Height = userProfile.Height + " " + userProfile.HeightUnit,
                    ActivityLevel = userProfile.ActivityLevel,
                },
                NutritionalGoals = new UserNutritionalGoals
                {
                    Goals = CombineLists(userProfile.MainGoals, userProfile.OtherMainGoals, "No specific Nutritional Goals"),
                    TargetWeight = userProfile.WeightGoal + userProfile.WeightGoalUnit,
                    PaceOfWeightChange = userProfile.Pace,
                    DietaryRestrictionsBasedOnFoodSource = CombineLists(userProfile.DietaryPreferences, userProfile.OtherDietaryPreferences, "No dietary restrictions based on food source specified"),
                    DietaryRestrictionsBasedOnMacroNutritients = CombineLists(userProfile.DietaryPreferencesMacros, userProfile.OtherDietaryPreferencesMacros, "No macro-nutrient restrictions specified"),
                    Allergies = CombineLists(userProfile.FoodAllergies, userProfile.OtherFoodAllergies, "No allergies specified"),
                    HealthConditions = CombineLists(userProfile.HealthConditions, userProfile.OtherHealthConditions, "No health conditions specified"),
                    MentalHealthConditions = CombineLists(userProfile.MentalHealthConditions, userProfile.OtherMentalHealthConditions, "No mental health conditions specified")
                },
                MealsAndGroceries = new UserMealsAndGroceryPreferences
                {
                    CuisineLikes = CombineLists(userProfile.CuisinePreferences, userProfile.OtherCuisinePreferences, "No specific cuisine preferences"),
                    FavoriteMealTypes = userProfile.FavoriteDishes.Select(dish => dish.Text).ToList(),
                    EssentialIngredients = userProfile.MustHaveIngredients.Select(ingredient => ingredient.Text).ToList(),
                    DislikedIngredients = userProfile.UnlikedIngredients.Select(ingredient => ingredient.Text).ToList(),
                    PreferredCookingTime = userProfile.IdealCookingTime,
                    MealPreparationStyle = userProfile.ServingPreference,
                    MeasurementForLiquids = userProfile.LiquidUnit,
                    MeasurementForTemperature = userProfile.TemperatureUnit,
                    MeasurementForWeightUnitForIngredients = userProfile.IngredientsWeightUnit
                }
            };

            return userData;
        }

        private static List<string> CombineLists(List<string> list, string other, string noItemsMessage)
        {
            var combinedList = new List<string>();
            if (list != null && list.Any())
            {
                combinedList.AddRange(list);
            }
            if (!string.IsNullOrWhiteSpace(other))
            {
                combinedList.Add(other);
            }
            if (!combinedList.Any())
            {
                combinedList.Add(noItemsMessage);
            }

            return combinedList;
        }
    }
}
