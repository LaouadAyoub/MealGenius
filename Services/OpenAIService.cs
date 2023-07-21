using MealGeniusBackend.Model_UI;
using MealGeniusBackend.Models.Model_UI;
using MealGeniusBackend.Models.ModelGPT;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using MealGeniusBackend.Models.Model2ndResponse;
using System;

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

            string firstPrompt = GenerateFirstPrompt(userInfos);
            string systemPrompt = GenerateSystemPrompt(userInfos);
            string exampleChatbotOutput = GenerateExampleChatbotOutput(userInfos);

            chat.AppendSystemMessage(systemPrompt);
            chat.AppendExampleChatbotOutput(exampleChatbotOutput);
            chat.AppendUserInput(firstPrompt);
            var firstPromptResponse = await chat.GetResponseFromChatbotAsync();
            var firstPromptResponseJson = ProcessResponse(firstPromptResponse);

            WeekPlan myWeekPlan = System.Text.Json.JsonSerializer.Deserialize<WeekPlan>(firstPromptResponseJson);
             

            //END OF FIRST PROMPT

            List<DailyRecipesContainer> FullweekPlan = new List<DailyRecipesContainer>();

            List<string> responseList = new List<string>();

            Parallel.ForEach(myWeekPlan.DayMealPlans, (dayMealPlan) =>
            {
                StringBuilder mealDetails = GenerateMealDetails(dayMealPlan);
                string secondPrompt = GenerateSecondPrompt(userInfos, mealDetails, dayMealPlan);
                tasks.Add(GetSecondPromptResponse(secondPrompt));
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                // Get the result from the task
                var secondPromptResponse = task.Result;
                var secondPromptResponseJson = ProcessResponse(secondPromptResponse);
                responseList.Add(secondPromptResponseJson);
                var dayFullMealPlan = System.Text.Json.JsonSerializer.Deserialize<DailyRecipesContainer>(secondPromptResponseJson);
                // Add the new PrepInstructions to the list
                FullweekPlan.Add(dayFullMealPlan);
            }

            var weekPlanUI = CreateWeekPlanUI(myWeekPlan, FullweekPlan);
            var jsonWeekPlanUI = System.Text.Json.JsonSerializer.Serialize(weekPlanUI);
            Console.WriteLine("Response list " + responseList.ToString());

            return jsonWeekPlanUI;
        }


        public WeekPlan_UI CreateWeekPlanUI(WeekPlan weekPlan, List<DailyRecipesContainer> prepInstructions)
        {
            var weekPlanUI = new WeekPlan_UI
            {
                DayMealPlans = new List<DayMealPlan_UI>()
            };

            List<DailyRecipes> flatList = prepInstructions.SelectMany(p => p.DailyRecipes).ToList();
            foreach (var dayMealPlan in weekPlan.DayMealPlans)
            {
                var dayMealPlanUI = new DayMealPlan_UI
                {
                    DayName = dayMealPlan.DayName,
                    Meals = new List<Meal_UI>()
                };

                foreach (var meal in dayMealPlan.Meals)
                {
                    var prepInstruction = flatList.FirstOrDefault(p => p.MealName == meal.MealName);

                    if (prepInstruction != null)
                    {
                        var mealUI = new Meal_UI
                        {
                            MealType = meal.MealType,
                            MealName = meal.MealName,
                            GroceryItems = prepInstruction.GroceryItems,
                            Instructions = prepInstruction.Instructions,
                            Macros = prepInstruction.MealMacros
                        };

                        dayMealPlanUI.Meals.Add(mealUI);
                    }
                }

                weekPlanUI.DayMealPlans.Add(dayMealPlanUI);
            }

            return weekPlanUI;
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
            //chat.RequestParameters.MaxTokens = 2000;
            chat.RequestParameters.Temperature = 0.2;
            chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
            return chat;
        }

        private string GenerateFirstPrompt(UserInfos userInfos)
        {
            var firstPrompt = new StringBuilder();
            firstPrompt.AppendLine("You are given the following C# classes representing a meal planning system:");
            firstPrompt.AppendLine("```csharp");
            firstPrompt.AppendLine("public class WeekPlan");
            firstPrompt.AppendLine("{");
            firstPrompt.AppendLine("    public List<DayMealPlan> DayMealPlans { get; set; }");
            firstPrompt.AppendLine("}");
            firstPrompt.AppendLine("");
            firstPrompt.AppendLine("public class DayMealPlan");
            firstPrompt.AppendLine("{");
            firstPrompt.AppendLine("    public string DayName;");
            firstPrompt.AppendLine("}");
            firstPrompt.AppendLine("");
            firstPrompt.AppendLine("public class Meal");
            firstPrompt.AppendLine("{");
            firstPrompt.AppendLine("    public string MealType { get; set; }");
            firstPrompt.AppendLine("    public string MealName { get; set; }");
            firstPrompt.AppendLine("}");
            firstPrompt.AppendLine("```");
            firstPrompt.AppendLine($"You have to generate meals based on these Infos: Cuisine Type: {userInfos.CuisineType}, Age: {userInfos.Age}, Gender: {userInfos.Gender}, Weight: {userInfos.Weight}, Height: {userInfos.Height}, Objective: {userInfos.Objective}, Allergies: {userInfos.Allergies}, Cooking Skill Level: {userInfos.CookingSkillLevel}, Preferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}, Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}, Health Conditions: {userInfos.HealthConditions}, Food Dislikes: {userInfos.FoodDislikes}, Preparation Time: {userInfos.PreparationTime}, Meal Frequency: {userInfos.MealFrequency}, Number Of People: {userInfos.NumberOfPeople}, Unit: {userInfos.Unit}, User Comments: {userInfos.UserComments}");
            firstPrompt.AppendLine("");
            firstPrompt.AppendLine($"Please generate a JSON representation of a WeekPlan object with 7 days,  each having {userInfos.MealFrequency} meals per day. This information will be useful for determining the quantity of ingredients needed for each meal. The JSON should include the names of the days and for each mealtime (e.g., \"Breakfast\", \"Lunch\", \"Dinner\", \"Snack\") a specific meal name (e.g., \"Pasta Bolognese\", \"Green Salad\"). Use a variety of meal names.");
            firstPrompt.AppendLine("");
            firstPrompt.AppendLine("IMPORTANT NOTE: Please return the JSON in a VERY COMPACT FORM without ANY unnecessary whitespace to minimize token usage.");

            return firstPrompt.ToString();
        }


        private string GenerateSystemPrompt(UserInfos userInfos)
        {
            var systemPrompt = new StringBuilder();
            systemPrompt.AppendLine("I am an AI trained to generate personalized meal plans based on a variety of user inputs. The system I am working within uses several classes to organize and manage this data, which includes: ");
            systemPrompt.AppendLine("- `UserInfos`, which contains various user details, such as dietary preferences, allergies, cuisine type, cooking skill level, and other relevant parameters.");
            systemPrompt.AppendLine("- `WeekPlan`, which represents a week-long meal plan containing `DayMealPlan` objects.");
            systemPrompt.AppendLine("- `DayMealPlan`, which represents a daily meal plan and contains `Meal` objects.");
            systemPrompt.AppendLine("- `Meal`, which represents a single meal and contains properties for the meal type and meal name.");
            systemPrompt.AppendLine("\nNow, based on the user's information:");
            systemPrompt.AppendLine($"- Cuisine Type: {userInfos.CuisineType}");
            systemPrompt.AppendLine($"- Age: {userInfos.Age}");
            systemPrompt.AppendLine($"- Gender: {userInfos.Gender}");
            systemPrompt.AppendLine($"- Weight: {userInfos.Weight}");
            systemPrompt.AppendLine($"- Height: {userInfos.Height}");
            systemPrompt.AppendLine($"- Objective: {userInfos.Objective}");
            systemPrompt.AppendLine($"- Allergies: {userInfos.Allergies}");
            systemPrompt.AppendLine($"- Cooking Skill Level: {userInfos.CookingSkillLevel}");
            systemPrompt.AppendLine($"- Preferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}");
            systemPrompt.AppendLine($"- Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}");
            systemPrompt.AppendLine($"- Health Conditions: {userInfos.HealthConditions}");
            systemPrompt.AppendLine($"- Food Dislikes: {userInfos.FoodDislikes}");
            systemPrompt.AppendLine($"- Preparation Time: {userInfos.PreparationTime}");
            systemPrompt.AppendLine($"- Meal Frequency: {userInfos.MealFrequency}");
            systemPrompt.AppendLine($"- Number Of People: {userInfos.NumberOfPeople}");
            systemPrompt.AppendLine($"- Unit: {userInfos.Unit}");
            systemPrompt.AppendLine($"- User Comments: {userInfos.UserComments}\n");
            systemPrompt.AppendLine($"I need to provide a 7-day meal plan with {userInfos.MealFrequency} meals per day, each with a specific meal name. The meals should align with the user's provided information and preferences.");

            return systemPrompt.ToString();
        }


        private string GenerateExampleChatbotOutput(UserInfos userInfos)
        {
            // Generating the chatbot output directly inside this function
            string rawJson = "{\"DayMealPlans\":[{\"DayName\":\"Monday\",\"Meals\":[{\"MealType\":\"Breakfast\",\"MealName\":\"Quinoa Breakfast Bowl\"},{\"MealType\":\"Lunch\",\"MealName\":\"Mediterranean Chickpea Salad\"},{\"MealType\":\"Dinner\",\"MealName\":\"Eggplant Parmesan\"}]}]}";

            StringBuilder sb = new StringBuilder();

            // User information
            sb.AppendLine("User Information:");
            sb.AppendLine($"Cuisine Type: {userInfos.CuisineType}");
            sb.AppendLine($"Age: {userInfos.Age}");
            sb.AppendLine($"Gender: {userInfos.Gender}");
            sb.AppendLine($"Weight: {userInfos.Weight}");
            sb.AppendLine($"Height: {userInfos.Height}");
            sb.AppendLine($"Objective: {userInfos.Objective}");
            sb.AppendLine($"Allergies: {userInfos.Allergies}");
            sb.AppendLine($"Cooking Skill Level: {userInfos.CookingSkillLevel}");
            sb.AppendLine($"Preferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}");
            sb.AppendLine($"Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}");
            sb.AppendLine($"Health Conditions: {userInfos.HealthConditions}");
            sb.AppendLine($"Food Dislikes: {userInfos.FoodDislikes}");
            sb.AppendLine($"Preparation Time: {userInfos.PreparationTime}");
            sb.AppendLine($"Meal Frequency: {userInfos.MealFrequency}");
            sb.AppendLine($"Number Of People: {userInfos.NumberOfPeople}");
            sb.AppendLine($"Unit: {userInfos.Unit}");
            sb.AppendLine($"User Comments: {userInfos.UserComments}");

            // Convert the raw JSON to a nicely formatted string
            var jsonObj = JObject.Parse(rawJson);
            string formattedJson = jsonObj.ToString(Formatting.Indented);

            sb.AppendLine("\nGenerated Meal Plan:");
            sb.AppendLine(formattedJson);

            return sb.ToString();
        }


        private StringBuilder GenerateMealDetails(DayMealPlan dayMealPlan)
        {
            StringBuilder mealDetails = new StringBuilder();
            for (int i = 0; i < dayMealPlan.Meals.Count; i++)
            {
                mealDetails.AppendLine($"Meal{i + 1} name : {dayMealPlan.Meals[i].MealName}");
            }
            return mealDetails;
        }

        private string GenerateSecondPrompt(UserInfos userInfos, StringBuilder mealDetails, DayMealPlan dayMealPlan)
        {
            StringBuilder secondPrompt = new StringBuilder();

            secondPrompt.AppendLine("You are a nutritionist assistant AI. Your task is to create a meal plan based on the user's preferences and nutritional needs. You have already been given the details of a few meals. Now, you are to generate a JSON representation of a `DailyRecipes` object based on these meals and the user's profile information.");

            secondPrompt.AppendLine($"The meal plan consists of {dayMealPlan.Meals.Count} meals with the following details:\n");
            secondPrompt.AppendLine(mealDetails.ToString());

            secondPrompt.AppendLine($"User Infos: Cuisine Type: {userInfos.CuisineType}, Age: {userInfos.Age}, Gender: {userInfos.Gender}, Weight: {userInfos.Weight}kg, Height: {userInfos.Height}cm, Objective: {userInfos.Objective}, Allergies: {userInfos.Allergies}, Cooking Skill Level: {userInfos.CookingSkillLevel}, Preferred Ingredients: {string.Join(", ", userInfos.PreferredIngredients)}, Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}, Health Conditions: {userInfos.HealthConditions}, Food Dislikes: {userInfos.FoodDislikes}, Preparation Time: {userInfos.PreparationTime}, Meal Frequency: {userInfos.MealFrequency}, Number Of People: {userInfos.NumberOfPeople}, Unit: {userInfos.Unit}, User Comments: {userInfos.UserComments}");

            secondPrompt.AppendLine("\nBased on these details, the `DailyRecipes` object should include:");
            secondPrompt.AppendLine("1. A `MealName` which is the name of the meal.");
            secondPrompt.AppendLine("2. A `GroceryItems` list, where each item is an object containing an `IngredientName`, a `Quantity` in grams, and a `Unit` which is 'g' for grams.");
            secondPrompt.AppendLine("3. An `Instructions` list, which contains the step-by-step preparation instructions for the meal.");
            secondPrompt.AppendLine("4. The `MealMacros` which should include the `Protein`, `Carbs`, `Fats`, and `Calories` for the meal.");

            secondPrompt.AppendLine("\nHere are the corresponding C# classes:\n");
            secondPrompt.AppendLine("```csharp");
            secondPrompt.AppendLine("public class DailyRecipesContainer");
            secondPrompt.AppendLine("{");
            secondPrompt.AppendLine("    public List<DailyRecipes> DailyRecipes { get; set; }");
            secondPrompt.AppendLine("}");
            secondPrompt.AppendLine("public class DailyRecipes");
            secondPrompt.AppendLine("{");
            secondPrompt.AppendLine("    public string MealName { get; set; }");
            secondPrompt.AppendLine("    public List<GroceryItem> GroceryItems { get; set; }");
            secondPrompt.AppendLine("    public List<string> Instructions { get; set; }");
            secondPrompt.AppendLine("    public MealMacros MealMacros { get; set; }");
            secondPrompt.AppendLine("}");
            secondPrompt.AppendLine("public class GroceryItem");
            secondPrompt.AppendLine("{");
            secondPrompt.AppendLine("    public string IngredientName { get; set; }");
            secondPrompt.AppendLine("    public double Quantity { get; set; }");
            secondPrompt.AppendLine("    public string Unit { get; set; }");
            secondPrompt.AppendLine("}");
            secondPrompt.AppendLine("public class MealMacros");
            secondPrompt.AppendLine("{");
            secondPrompt.AppendLine("    public int Protein { get; set; }");
            secondPrompt.AppendLine("    public int Carbs { get; set; }");
            secondPrompt.AppendLine("    public int Fats { get; set; }");
            secondPrompt.AppendLine("    public int Calories { get; set; }");
            secondPrompt.AppendLine("}");
            secondPrompt.AppendLine("```\n");
            secondPrompt.AppendLine("Please make sure the JSON follows the format of the `DailyRecipes`, `GroceryItem`, `MealMacros` and `UserInfos` C# classes and is in a compact form with no unnecessary whitespace.");
            secondPrompt.AppendLine($"Please generate a JSON representation that follows the format of the `DailyRecipes`,  each having {userInfos.MealFrequency} meals per day. The number of people for whom this week plan is intended is {userInfos.NumberOfPeople}. This information will be useful for determining the quantity of ingredients needed for each meal.");
            secondPrompt.AppendLine("NOTE: Generate the `DailyRecipes` based on the user preferences specified in the `UserInfos`. Choose the meal quantities and `MealMacros` based on the user's `Objective`, `Weight`, `Height`, `Age`, `Gender`, `MealFrequency`, `NumberOfPeople`, and the `Unit`. Consider also the `CuisineType`, `Allergies`, `CookingSkillLevel`, `PreferredIngredients`, `DietaryPreferencesRestrictions`, `HealthConditions`, `FoodDislikes`, and `PreparationTime`.");



            return secondPrompt.ToString();
        }



        private async Task<string> GetSecondPromptResponse(string secondPrompt)
        {
            APIAuthentication.Default = new APIAuthentication(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            var aOpenAiAPI = new OpenAIAPI();
            var aChat = CreateConversation(aOpenAiAPI);
            aChat.AppendUserInput(secondPrompt);
            var exampleChatbotOutput = @"
                [
                    {
                        ""MealName"": """",
                        ""GroceryItems"": [
                            {
                                ""IngredientName"": """",
                                ""Quantity"": null,
                                ""Unit"": """"
                            },
                            ...
                        ],
                        ""Instructions"": [
                            """",
                            ...
                        ],
                        ""MealMacros"": {
                            ""Protein"": null,
                            ""Carbs"": null,
                            ""Fats"": null,
                            ""Calories"": null
                        }
                    },
                    ...
                ]
            ";


            //aChat.AppendExampleChatbotOutput(exampleChatbotOutput);

            var response = await aChat.GetResponseFromChatbotAsync();
            return response;
        }


    }
}
