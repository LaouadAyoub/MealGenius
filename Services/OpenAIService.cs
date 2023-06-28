using MealGeniusBackend.Model_UI;
using MealGeniusBackend.ModelGPT;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text.Json;

namespace MealGeniusBackend.Services
{
    public interface IOpenAIService
    {
        Task<string> GetMealPlan(UserInfos userInfos);
    }

    public partial class OpenAIService : IOpenAIService
    {
        private readonly OpenAIAPI _openAiApi;
        public OpenAIService(OpenAIAPI openAiApi)
        {
            _openAiApi = openAiApi;
        }

        public async Task<string> GetMealPlan(UserInfos userInfos)
        {
            List<Task<string>> tasks = new List<Task<string>>();
            var chat = CreateConversation(_openAiApi);

            string firstPrompt = "You are given the following C# classes representing a meal planning system: \n" +
                                "```csharp \n" +
                                "public class WeekPlan \n" +
                                "{ \n" +
                                "    public List<DayMealPlan> DayMealPlans { get; set; } \n" +
                                "} \n" +
                                "\n" +
                                "public class DayMealPlan \n" +
                                "{ \n" +
                                "    public string DayName; \n" +
                                "} \n" +
                                "\n" +
                                "public class Meal \n" +
                                "{ \n" +
                                "    public string MealType { get; set; } \n" +
                                "    public string MealName { get; set; } \n" +
                                "} \n" +
                                "``` \n" +
                                "Please generate a JSON representation of a WeekPlan object with 7 days, each having " + userInfos.MealFrequency + " meals per day. The JSON should include the names of the days and for each mealtime (e.g., \"Breakfast\", \"Lunch\", \"Dinner\", \"Snack\") a specific meal name (e.g., \"Pasta Bolognese\", \"Green Salad\"). Use a variety of meal names. \n" +
                                "\n" +
                                "IMPORTANT NOTE: Please return the JSON in a VERY COMPACT FORM without ANY unnecessary whitespace to minimize token usage.";


            chat.AppendSystemMessage($"As an AI trained to provide meal planning assistance, you have been given the following user information and preferences in the following json object called 'userInput':\n{userInfos}\nBased on this information, please generate a 7-day meal plan. The number of meals/day is the 'Meal Frequency' key in 'userInput' json object.");
            chat.AppendExampleChatbotOutput($"Based on the user preferences, here is a summary:\n\nCuisine Type: {userInfos.CuisineType}\nAge: {userInfos.Age}\nGender: {userInfos.Gender}\nWeight: {userInfos.Weight} kg\nHeight: {userInfos.Height} cm\nObjective: {userInfos.Objective}\nAllergies: {userInfos.Allergies}\nCooking Skill Level: {userInfos.CookingSkillLevel}\nPreferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}\nDietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}\nHealth Conditions: {userInfos.HealthConditions}\nFood Dislikes: {userInfos.FoodDislikes}\nPreparation Time: {userInfos.PreparationTime}\nMeal Frequency: {userInfos.MealFrequency} meals/day\nNumber of People: {userInfos.NumberOfPeople}\n\nNow, I will generate a 7-day meal plan based on these preferences.");
            chat.AppendUserInput(firstPrompt);
            var response = await chat.GetResponseFromChatbotAsync();

            var jsonResonse = ProcessResponse(response);

            WeekPlan myWeekPlan = JsonSerializer.Deserialize<WeekPlan>(jsonResonse);

            List<PrepInstructions> prepInstructionsList = new List<PrepInstructions>();
            TotalGroceryList totalGroceryList = new TotalGroceryList
            {
                GroceryItems = new List<GroceryItem>()
            };
            List<string> responseList = new List<string>();
            foreach (var dayMealPlan in myWeekPlan.DayMealPlans)
            {
                foreach (var meal in dayMealPlan.Meals)
                {
                    var mealType = meal.MealType;
                    var mealName = meal.MealName;

                    string secondPrompt = $"Forget every thing that was said before. You are given a meal with the following details:\n\n" +
                                          $"Meal Type: {mealType}\n" +
                                          $"Meal Name: {mealName}\n\n" +
                                          $"User Infos: {userInfos}\n\n" +
                                          "Based on these details, please generate a JSON representation of a `PrepInstructions` object. " +
                                          "This object should include:\n\n" +
                                          "1. The `MealName` which is the name of the meal.\n" +
                                          "2. A `GroceryItems` list, with each item in the list being a `GroceryItem` object. Each `GroceryItem` should have an `IngredientName`, a `Quantity` in grams, and a `Unit` which is 'g' for grams.\n" +
                                          "3. An `Instructions` list that contains the step-by-step preparation instructions for the meal.\n" +
                                          "4. The `Macros` for the meal, which include the `Protein`, `Carbs`, `Fats`, and `Calories`.\n\n" +
                                          "Here are the corresponding C# classes:\n\n" +
                                          "```csharp\n" +
                                          "public class PrepInstructions\n" +
                                          "{\n" +
                                          "    public string MealName { get; set; }\n" +
                                          "    public List<GroceryItem> GroceryItems { get; set; }\n" +
                                          "    public List<string> Instructions { get; set; }\n" +
                                          "    public Macros MealMacros { get; set; }\n" +
                                          "}\n\n" +
                                          "public class GroceryItem\n" +
                                          "{\n" +
                                          "    public string IngredientName { get; set; }\n" +
                                          "    public double Quantity { get; set; }\n" +
                                          "    public string Unit { get; set; }\n" +
                                          "}\n\n" +
                                          "public class Macros\n" +
                                          "{\n" +
                                          "    public float Protein { get; set; }\n" +
                                          "    public float Carbs { get; set; }\n" +
                                          "    public float Fats { get; set; }\n" +
                                          "    public int Calories { get; set; }\n" +
                                          "}\n" +
                                          "```\n\n" +
                                          "Please make sure the JSON follows the format of the `PrepInstructions`, `GroceryItem`, and `Macros` C# classes and is in a compact form with no unnecessary whitespace.\n\n" +
                                          "NOTE: Choose the meal quantities based on the information in the `UserInfos` and speciallfy the UserInfos.Number of people and the objectif of the user";

                    APIAuthentication.Default = new APIAuthentication(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                    var aOpenAiAPI = new OpenAIAPI();
                    var aChat = CreateConversation(aOpenAiAPI);
                    aChat.AppendSystemMessage(secondPrompt);
                    tasks.Add(GetResponseFromChatbotAsync(aChat));
                }
            }
            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                // Get the result from the task
                var response2 = task.Result;
                var jsonResonse2 = ProcessResponse(response2);
                responseList.Add(jsonResonse2);
                PrepInstructions myPrepInstructions = JsonSerializer.Deserialize<PrepInstructions>(jsonResonse2);
                // Add the new PrepInstructions to the list
                prepInstructionsList.Add(myPrepInstructions);
            }
            
            var weekPlanUI = CreateWeekPlanUI(myWeekPlan, prepInstructionsList);
            var jsonWeekPlanUI = JsonSerializer.Serialize(weekPlanUI);
            Console.WriteLine("Response list " + responseList.ToString());
           // Console.WriteLine("WeekPlan_UI in JSON format: " + jsonWeekPlanUI);

            return jsonWeekPlanUI;
        }

        public WeekPlan_UI CreateWeekPlanUI(WeekPlan weekPlan, List<PrepInstructions> prepInstructions)
        {
            var weekPlanUI = new WeekPlan_UI
            {
                DayMealPlans = new List<DayMealPlan_UI>()
            };

            foreach (var dayMealPlan in weekPlan.DayMealPlans)
            {
                var dayMealPlanUI = new DayMealPlan_UI
                {
                    DayName = dayMealPlan.DayName,
                    Meals = new List<Meal_UI>()
                };

                foreach (var meal in dayMealPlan.Meals)
                {
                    var prepInstruction = prepInstructions.FirstOrDefault(p => p.MealName == meal.MealName);

                    if (prepInstruction != null)
                    {
                        var mealUI = new Meal_UI
                        {
                            MealType = meal.MealType,
                            MealName = meal.MealName,
                            GroceryItems = prepInstruction.GroceryItems,
                            Instructions = prepInstruction.Instructions,
                            MealMacros = prepInstruction.MealMacros
                        };

                        dayMealPlanUI.Meals.Add(mealUI);
                    }
                }

                weekPlanUI.DayMealPlans.Add(dayMealPlanUI);
            }

            return weekPlanUI;
        }

        private async Task<string> GetResponseFromChatbotAsync(Conversation chat)
        {
            var response = await chat.GetResponseFromChatbotAsync();
            return response;
        }

        private string ProcessResponse(string response)
        {
            int firstCurlyBracePosition = response.IndexOf('{');
            int lastCurlyBracePosition = response.LastIndexOf('}');
            if (firstCurlyBracePosition >= 0 && lastCurlyBracePosition >= 0)
            {
                return response.Substring(firstCurlyBracePosition, lastCurlyBracePosition - firstCurlyBracePosition + 1); // +1 to include the '}' itself
            }
            throw new InvalidOperationException("The expected JSON response was not found in the chatbot's response.");
        }

        private Conversation CreateConversation(OpenAIAPI iOpenAIAPI)
        {
            var chat = iOpenAIAPI.Chat.CreateConversation();
            chat.RequestParameters.MaxTokens = 2000;
            chat.RequestParameters.Temperature = 0.2;
            chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
            return chat;
        }

    }
}
