using MealGeniusBackend.Models.ModelGPT;

namespace MealGeniusBackend.Models
{
    public static class Prompts
    {
        public static string GetFirstPrompt(UserInfos userInfos)
        {
            string userDetails = $"Cuisine Type: {userInfos.CuisineType}, Age: {userInfos.Age}, Gender: {userInfos.Gender}, Weight: {userInfos.Weight}, Height: {userInfos.Height}, Objective: {userInfos.Objective}, Allergies: {userInfos.Allergies}, Cooking Skill Level: {userInfos.CookingSkillLevel}, Preferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}, Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}, Health Conditions: {userInfos.HealthConditions}, Food Dislikes: {userInfos.FoodDislikes}, Preparation Time: {userInfos.PreparationTime}, Meal Frequency: {userInfos.MealFrequency}, Number Of People: {userInfos.NumberOfPeople}, Unit: {userInfos.Unit}, User Comments: {userInfos.UserComments}";

            string firstPrompt = "You are given the following C# classes representing a meal planning system: \n" +
                                /* ... rest of the firstPrompt ... */
                                "IMPORTANT NOTE: Please return the JSON in a VERY COMPACT FORM without ANY unnecessary whitespace to minimize token usage.";

            return firstPrompt;
        }
        // ... Other prompts ...
    }

}
