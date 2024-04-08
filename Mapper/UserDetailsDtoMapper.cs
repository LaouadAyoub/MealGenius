using MealGeniusBackend.Models;

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
                    SexOrGender = (userProfile.Sexe== "Other") ? userProfile.OtherSexeDetails : userProfile.Sexe,
                    Weight = Convert.ToDouble(userProfile.Weight),
                    Height = Convert.ToDouble(userProfile.Height),
                    WeightUnit = userProfile.WeightUnit,
                    HeightUnit = userProfile.HeightUnit,
                    ActivityLevel = userProfile.ActivityLevel,
                },
                NutritionalGoals = new UserNutritionalGoals
                {
                    Goals = CombineLists(userProfile.MainGoals, userProfile.OtherMainGoals),
                    TargetWeight = Convert.ToDouble(userProfile.WeightGoal),
                    PaceOfWeightChange = userProfile.Pace,
                    DietaryRestrictionsBasedOnFoodSource = CombineLists(userProfile.DietaryPreferences, userProfile.OtherDietaryPreferences),
                    DietaryRestrictionsBasedOnMacroNutritients = CombineLists(userProfile.DietaryPreferencesMacros, userProfile.OtherDietaryPreferencesMacros),
                    Allergies = CombineLists(userProfile.FoodAllergies, userProfile.OtherFoodAllergies),
                    HealthConditions = CombineLists(userProfile.HealthConditions, userProfile.OtherHealthConditions),
                    MentalHealthConditions = CombineLists(userProfile.MentalHealthConditions, userProfile.OtherMentalHealthConditions)
                },
                MealsAndGroceries = new UserMealsAndGroceryPreferences
                {
                    CuisineLikes = CombineLists(userProfile.CuisinePreferences, userProfile.OtherCuisinePreferences),
                    FavoriteMealTypes = userProfile.FavoriteDishes.Select(dish => dish.Text).ToList(),
                    EssentialIngredients = userProfile.MustHaveIngredients.Select(ingredient => ingredient.Text).ToList(),
                    DislikedIngredients = userProfile.UnlikedIngredients.Select(ingredient => ingredient.Text).ToList(),
                    PreferredCookingTime = userProfile.IdealCookingTime,
                    MealPreparationStyle = userProfile.ServingPreference,
                    MeasurementForLiquids = userProfile.LiquidUnit,
                    MeasurementForTemperature = userProfile.TemperatureUnit,
                    MeasurementForWeightUnit = userProfile.WeightUnit
                }
            };

            return userData;
        }

        private static List<string> CombineLists(List<string> list, string other)
        {
            var combinedList = new List<string>();
            if (list != null)
            {
                combinedList.AddRange(list);
            }

            if (!string.IsNullOrWhiteSpace(other))
            {
                combinedList.Add(other);
            }

            return combinedList;
        }

    }
}
