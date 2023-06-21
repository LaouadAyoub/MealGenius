using MealGeniusBackend.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using System;
using System.Text.Json;
using Newtonsoft.Json;

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPI : ControllerBase
    {
        private readonly OpenAIAPI _openAiApi;

        public MainAPI(OpenAIAPI openAiApi)
        {
            _openAiApi = openAiApi;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMeal([FromBody] UserInfos userInfos)
        {
            var apiKey = "REDACTED";
            var api = new OpenAI_API.OpenAIAPI(apiKey);
            var chat = api.Chat.CreateConversation();
            chat.RequestParameters.MaxTokens = 500;
            chat.RequestParameters.Temperature = 0.2;
            chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;

            chat.AppendSystemMessage($"As an AI trained to provide meal planning assistance, you have been given the following user information and preferences in the following json object called 'userInput':\n{userInfos}\nBased on this information, please generate a 7-day meal plan. The number of meals/day is the 'Meal Frequency' key in 'userInput' json object.");
            chat.AppendUserInput($"Please, before generating the output, rewrite a summary of the user preferences");
            chat.AppendExampleChatbotOutput($"Based on the user preferences, here is a summary:\n\nCuisine Type: {userInfos.CuisineType}\nAge: {userInfos.Age}\nGender: {userInfos.Gender}\nWeight: {userInfos.Weight} kg\nHeight: {userInfos.Height} cm\nObjective: {userInfos.Objective}\nAllergies: {userInfos.Allergies}\nCooking Skill Level: {userInfos.CookingSkillLevel}\nPreferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}\nDietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}\nHealth Conditions: {userInfos.HealthConditions}\nFood Dislikes: {userInfos.FoodDislikes}\nPreparation Time: {userInfos.PreparationTime}\nMeal Frequency: {userInfos.MealFrequency} meals/day\nNumber of People: {userInfos.NumberOfPeople}\n\nNow, I will generate a 7-day meal plan based on these preferences.");
            chat.AppendUserInput($"Generate a json object that begins with a key named 'startFlag' with the value '#TheGenerationHasStarted' and ends with a key named 'endFlag' with the value '#TheGenerationIsFinished'. The object should contain a 7-day meal plan with each day being a separate object with keys 'Breakfast', 'Lunch', 'Dinner' and 'Snack'. Each day should consist of various meals. The JSON should follow this format: {{\"startFlag\":\"#TheGenerationHasStarted\",\"Monday\":{{\"Breakfast\":\"...\",\"Lunch\":\"...\",\"Dinner\":\"...\",\"Snack\":\"...\"}},...,\"endFlag\":\"#TheGenerationIsFinished\"}}. Please ensure that the output contains only the JSON object with no additional text before or after it.");

            var chatResponse = await chat.GetResponseFromChatbotAsync();
            int startJson = chatResponse.IndexOf("\"startFlag\": \"#TheGenerationHasStarted\"");
            int endJson = chatResponse.IndexOf("\"endFlag\": \"#TheGenerationIsFinished\"") + "\"endFlag\": \"#TheGenerationIsFinished\"".Length;

            // Check if flags were found
            if (startJson == -1 || endJson == -1)
            {
                Console.WriteLine("Could not find the startFlag or endFlag in the response.");
            }

            // Extract the JSON string
            string jsonString = chatResponse.Substring(startJson-1, endJson - startJson);

            // Deserialize the JSON string into a MealPlan object
            var mealPlan = JsonConvert.DeserializeObject<MealPlan>(jsonString);


            // Log the final result
            Console.WriteLine("Meal plan created successfully! \n "+ chatResponse);

            // Return the final response as JSON
            return Ok(chatResponse);
        }

        private static string ExtractJson(string response)
        {
            int startIndex = response.IndexOf("```json\n") + 8; // Add 8 to remove "```json\n" from the start index.
            int endIndex = response.IndexOf("```", startIndex);
            return response.Substring(startIndex, endIndex - startIndex);
        }


    }
}
