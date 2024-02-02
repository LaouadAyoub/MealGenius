using MealGeniusBackend.Model_UI;
using MealGeniusBackend.Models.Model_UI;
using MealGeniusBackend.Models.Model2ndResponse;
using MealGeniusBackend.Models.ModelGPT;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text;

namespace MealGeniusBackend.Services
{
    public interface IOpenAIService
    {
        Task<WeekPlan_UI> GetMealPlan(UserInputDataModel inputData);
    }

    public partial class OpenAIService : IOpenAIService
    {
        private readonly OpenAIAPI _openAiApi;
        private readonly ILogger<OpenAIService> _logger;  // Add this line

        public OpenAIService(OpenAIAPI openAiApi, ILogger<OpenAIService> logger)
        {
            _openAiApi = openAiApi;
            _logger = logger;  // Assign logger

        }

        public async Task<WeekPlan_UI> GetMealPlan(UserInputDataModel inputData)
        {
            try
            {
                _logger.LogInformation("GetMealRecipe: Starting GetMealRecipe");

                List<Task<string>> tasks = new List<Task<string>>();
                var chat = CreateConversation(_openAiApi);

                string firstPrompt = GenerateFirstPrompt(inputData);
                string systemPrompt = GenerateSystemPrompt(inputData);
                string exampleChatbotOutput = GenerateExampleChatbotOutput(inputData);

                chat.AppendSystemMessage(systemPrompt);
                //chat.AppendExampleChatbotOutput(exampleChatbotOutput);
                chat.AppendUserInput(firstPrompt);
                var firstPromptResponse = await chat.GetResponseFromChatbotAsync();
                var firstPromptResponseJson = ProcessResponse(firstPromptResponse);

                WeekPlan myWeekPlan = System.Text.Json.JsonSerializer.Deserialize<WeekPlan>(firstPromptResponseJson);

                _logger.LogInformation("GetMealRecipe: END OF FIRST PROMPT");

                //END OF FIRST PROMPT

                List<MealRecipes> FullweekPlan = new List<MealRecipes>();

                List<string> responseList = new List<string>();

                //foreach (var dayMealPlan in myWeekPlan.DayMealPlans)

                //Parallel.ForEach(myWeekPlan.DayMealPlans, (dayMealPlan) =>
                foreach (var dayMealPlan in myWeekPlan.DayMealPlans)
                {
                    foreach (var meal in dayMealPlan.Meals)
                    {
                        //StringBuilder mealDetails = GenerateMealDetails(meal); //Assuming GenerateMealDetails() can handle individual meals
                        string secondPrompt = GenerateSecondPrompt_PRO(inputData, meal); //Assuming GenerateSecondPrompt() can handle individual meals
                        tasks.Add(GetSecondPromptResponse(secondPrompt));
                    }
                }
                //});


                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                foreach (var task in tasks)
                {
                    // Get the result from the task
                    var secondPromptResponse = task.Result;
                    var secondPromptResponseJson = ProcessResponse(secondPromptResponse);
                    responseList.Add(secondPromptResponseJson);
                    var dayFullMealPlan = System.Text.Json.JsonSerializer.Deserialize<MealRecipes>(secondPromptResponseJson);
                    // Add the new PrepInstructions to the list
                    FullweekPlan.Add(dayFullMealPlan);
                }
                _logger.LogInformation("GetMealRecipe: second prompt Completed ");

                var weekPlanUI = CreateWeekPlanUI(myWeekPlan, FullweekPlan);

                return weekPlanUI;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating meal plan.");
                throw;
            }
        }


        public WeekPlan_UI CreateWeekPlanUI(WeekPlan weekPlan, List<MealRecipes> prepInstructions)
        {
            try
            {
                _logger.LogInformation("CreateWeekPlanUI: Started creating WeekPlanUI");
                var weekPlanUI = new WeekPlan_UI
                {
                    DayMealPlans = new List<DayMealPlan_UI>()
                };

                //List<MealRecipes> flatList = prepInstructions.SelectMany(p => p.DayMealPlans).ToList();

                string[] weekDays = new string[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

                int dayIndex = 0;
                foreach (var dayMealPlan in weekPlan.DayMealPlans)
                {
                    var dayMealPlanUI = new DayMealPlan_UI
                    {
                        DayName = weekDays[dayIndex % 7],
                        Meals = new List<Meal_UI>()
                    };
                    dayIndex++;
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
                                Macros = prepInstruction.MealMacros
                            };

                            dayMealPlanUI.Meals.Add(mealUI);
                        }
                    }

                    weekPlanUI.DayMealPlans.Add(dayMealPlanUI);
                }
                _logger.LogInformation("CreateWeekPlanUI: Started creating WeekPlanUI");
                return weekPlanUI;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating WeekPlanUI.");
                throw;
            }
        }

        private string ProcessResponse(string response)
        {
            _logger.LogInformation("ProcessResponse: Started processing response");

            int firstCurlyBracePosition = response.IndexOf('{');
            int lastCurlyBracePosition = response.LastIndexOf('}');
            if (firstCurlyBracePosition >= 0 && lastCurlyBracePosition >= 0)
            {
                var result = response.Substring(firstCurlyBracePosition, lastCurlyBracePosition - firstCurlyBracePosition + 1); // +1 to include the '}' itself
                _logger.LogInformation("ProcessResponse: Started processing response");
                return result;
            }
            _logger.LogInformation("ProcessResponse: Successfully processed response");
            throw new InvalidOperationException("The expected JSON response was not found in the chatbot's response.");
        }

        private Conversation CreateConversation(OpenAIAPI iOpenAIAPI)
        {
            _logger.LogInformation("CreateConversation: Starting creation of new conversation");

            try
            {
                var chat = iOpenAIAPI.Chat.CreateConversation();

                chat.RequestParameters.Temperature = 0.2;
                chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;

                _logger.LogInformation("CreateConversation: Successfully created a new conversation");

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError("CreateConversation: An error occurred while creating conversation: {Message}", ex.Message);
                throw;
            }
        }


        private string GenerateFirstPrompt(UserInputDataModel userInfos)
        {
            _logger.LogInformation("GenerateFirstPrompt: Starting first prompt generation");
            try
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
                firstPrompt.AppendLine($"You have to generate meals based on these Infos: Cuisine Type: {userInfos.CuisineType}, Age: {userInfos.Age}, Gender: {userInfos.Gender}, Weight: {userInfos.Weight}, Height: {userInfos.Height}, Objective: {userInfos.Objective}, Allergies: {userInfos.Allergies}, Cooking Skill Level: {userInfos.CookingSkillLevel}, Preferred ingredients: {string.Join(", ", userInfos.PreferredIngredients)}, Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}, Health Conditions: {userInfos.HealthConditions}, Food Dislikes: {userInfos.FoodDislikes}, Preparation Time: {userInfos.PreparationTime}, Meal Frequency: {userInfos.MealFrequency} meals/day, Number Of People: {userInfos.NumberOfPeople}, Unit: {userInfos.Unit}, User Comments: {userInfos.UserComments}");
                firstPrompt.AppendLine("");
                firstPrompt.AppendLine($"Please generate a JSON representation of a WeekPlan object with 7 days,  each having {userInfos.MealFrequency} meals per day. This information will be useful for determining the quantity of ingredients needed for each meal. The JSON should include the names of the days and for each mealtime (e.g., \"Breakfast\", \"Lunch\", \"Dinner\", \"Snack\") a specific meal name (e.g., \"Pasta Bolognese\", \"Green Salad\"). Use a variety of meal names.");
                firstPrompt.AppendLine("");
                firstPrompt.AppendLine("IMPORTANT NOTE: Please return the JSON in a VERY COMPACT FORM without ANY unnecessary whitespace to minimize token usage.");

                string prompt = firstPrompt.ToString();
                _logger.LogInformation("GenerateFirstPrompt: Successfully generated first prompt");
                _logger.LogDebug("GenerateFirstPrompt: First prompt contents: {Prompt}", prompt);

                return prompt;
            }
            catch (Exception ex)
            {
                _logger.LogError("GenerateFirstPrompt: An error occurred while generating first prompt: {Message}", ex.Message);
                throw;
            }
        }

        private string GenerateSystemPrompt(UserInputDataModel inputData)
        {
            _logger.LogInformation("GenerateSystemPrompt: Starting system prompt generation");
            try
            {
                var systemPrompt = new StringBuilder();
                systemPrompt.AppendLine("I am an AI trained to generate personalized meal plans based on a variety of user inputs. The system I am working within uses several classes to organize and manage this data, which includes: ");
                systemPrompt.AppendLine("- `UserInfos`, which contains various user details, such as dietary preferences, allergies, cuisine type, cooking skill level, and other relevant parameters.");
                systemPrompt.AppendLine("- `WeekPlan`, which represents a week-long meal plan containing `DayMealPlan` objects.");
                systemPrompt.AppendLine("- `DayMealPlan`, which represents a daily meal plan and contains `Meal` objects.");
                systemPrompt.AppendLine("- `Meal`, which represents a single meal and contains properties for the meal type and meal name.");
                systemPrompt.AppendLine("\nNow, based on the user's information:");
                systemPrompt.AppendLine($"- Cuisine Type: {inputData.CuisineType}");
                systemPrompt.AppendLine($"- Age: {inputData.Age}");
                systemPrompt.AppendLine($"- Gender: {inputData.Gender}");
                systemPrompt.AppendLine($"- Weight: {inputData.Weight}");
                systemPrompt.AppendLine($"- Height: {inputData.Height}");
                systemPrompt.AppendLine($"- Objective: {inputData.Objective}");
                systemPrompt.AppendLine($"- Allergies: {inputData.Allergies}");
                systemPrompt.AppendLine($"- Cooking Skill Level: {inputData.CookingSkillLevel}");
                systemPrompt.AppendLine($"- Preferred ingredients: {string.Join(", ", inputData.PreferredIngredients)}");
                systemPrompt.AppendLine($"- Dietary Preferences/Restrictions: {inputData.DietaryPreferencesRestrictions}");
                systemPrompt.AppendLine($"- Health Conditions: {inputData.HealthConditions}");
                systemPrompt.AppendLine($"- Food Dislikes: {inputData.FoodDislikes}");
                systemPrompt.AppendLine($"- Preparation Time: {inputData.PreparationTime}");
                systemPrompt.AppendLine($"- Meal Frequency: {inputData.MealFrequency} meals/day");
                systemPrompt.AppendLine($"- Number Of People: {inputData.NumberOfPeople}");
                systemPrompt.AppendLine($"- Unit: {inputData.Unit}");
                systemPrompt.AppendLine($"- User Comments: {inputData.UserComments}\n");
                systemPrompt.AppendLine($"I need to provide a 7-day meal plan with {inputData.MealFrequency} meals per day, each with a specific meal name. The meals should align with the user's provided information and preferences.");

                string prompt = systemPrompt.ToString();
                _logger.LogInformation("GenerateSystemPrompt: Successfully generated system prompt");
                _logger.LogDebug("GenerateSystemPrompt: System prompt contents: {Prompt}", prompt);

                return prompt;
            }
            catch (Exception ex)
            {
                _logger.LogError("GenerateSystemPrompt: An error occurred while generating system prompt: {Message}", ex.Message);
                throw;
            }
        }

        private string GenerateExampleChatbotOutput(UserInputDataModel inputData)
        {
            _logger.LogInformation("GenerateExampleChatbotOutput: Starting generation of example chatbot output");
            try
            {
                // Generating the chatbot output directly inside this function
                string rawJson = "{\"DayMealPlans\":[{\"DayName\":\"Monday\",\"Meals\":[{\"MealType\":\"...\",\"MealName\":\"...\"}]}]}";

                StringBuilder sb = new StringBuilder();

                // User information
                sb.AppendLine("User Information:");
                sb.AppendLine($"Cuisine Type: {inputData.CuisineType}");
                sb.AppendLine($"Age: {inputData.Age}");
                sb.AppendLine($"Gender: {inputData.Gender}");
                sb.AppendLine($"Weight: {inputData.Weight}");
                sb.AppendLine($"Height: {inputData.Height}");
                sb.AppendLine($"Objective: {inputData.Objective}");
                sb.AppendLine($"Allergies: {inputData.Allergies}");
                sb.AppendLine($"Cooking Skill Level: {inputData.CookingSkillLevel}");
                sb.AppendLine($"Preferred ingredients: {string.Join(", ", inputData.PreferredIngredients)}");
                sb.AppendLine($"Dietary Preferences/Restrictions: {inputData.DietaryPreferencesRestrictions}");
                sb.AppendLine($"Health Conditions: {inputData.HealthConditions}");
                sb.AppendLine($"Food Dislikes: {inputData.FoodDislikes}");
                sb.AppendLine($"Preparation Time: {inputData.PreparationTime}");
                sb.AppendLine($"Meal Frequency: {inputData.MealFrequency}");
                sb.AppendLine($"Number Of People: {inputData.NumberOfPeople}");
                sb.AppendLine($"Unit: {inputData.Unit}");
                sb.AppendLine($"User Comments: {inputData.UserComments}");

                // Convert the raw JSON to a nicely formatted string
                var jsonObj = JObject.Parse(rawJson);
                string formattedJson = jsonObj.ToString(Formatting.Indented);

                sb.AppendLine("\nGenerated Meal Plan:");
                sb.AppendLine(formattedJson);

                string output = sb.ToString();
                _logger.LogInformation("GenerateExampleChatbotOutput: Successfully generated example chatbot output");
                _logger.LogDebug("GenerateExampleChatbotOutput: Example chatbot output: {Output}", output);

                return output;
            }
            catch (Exception ex)
            {
                _logger.LogError("GenerateExampleChatbotOutput: An error occurred while generating example chatbot output: {Message}", ex.Message);
                throw;
            }
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

        private string GenerateSecondPrompt(UserInputDataModel userInfos, StringBuilder mealDetails, DayMealPlan dayMealPlan)
        {
            _logger.LogInformation("GenerateSecondPrompt: Starting generation of second prompt");
            try
            {
                StringBuilder secondPrompt = new StringBuilder();

                secondPrompt.AppendLine("You are a nutritionist assistant AI. Your task is to create a meal plan based on the user's preferences and nutritional needs. You have already been given the details of a few meals. Now, you are to generate a JSON representation of a `MealRecipes` object based on these meals and the user's profile information.");

                secondPrompt.AppendLine($"The meal plan consists of {dayMealPlan.Meals.Count} meals with the following details:\n");
                secondPrompt.AppendLine(mealDetails.ToString());

                secondPrompt.AppendLine($"User Infos: Cuisine Type: {userInfos.CuisineType}, Age: {userInfos.Age}, Gender: {userInfos.Gender}, Weight: {userInfos.Weight}kg, Height: {userInfos.Height}cm, Objective: {userInfos.Objective}, Allergies: {userInfos.Allergies}, Cooking Skill Level: {userInfos.CookingSkillLevel}, Preferred ingredients: {string.Join(", ", userInfos.PreferredIngredients)}, Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}, Health Conditions: {userInfos.HealthConditions}, Food Dislikes: {userInfos.FoodDislikes}, Preparation Time: {userInfos.PreparationTime}, Meal Frequency: {userInfos.MealFrequency}, Number Of People: {userInfos.NumberOfPeople}, Unit: {userInfos.Unit}, User Comments: {userInfos.UserComments}");

                secondPrompt.AppendLine("\nBased on these details, the `MealRecipes` object should include:");
                secondPrompt.AppendLine("1. A `MealName` which is the name of the meal.");
                secondPrompt.AppendLine("2. A `GroceryItems` list, where each item is an object containing an `IngredientName`, a `Quantity` in grams, and a `Unit` which is 'g' for grams.");
                secondPrompt.AppendLine("3. An `Instructions` list, which contains the step-by-step preparation instructions for the meal.");
                secondPrompt.AppendLine("4. The `MealMacros` which should include the `Protein`, `Carbs`, `Fats`, and `Calories` for the meal.");

                secondPrompt.AppendLine("\nHere are the corresponding C# classes:\n");
                secondPrompt.AppendLine("```csharp");
                secondPrompt.AppendLine("public class DailyRecipes");
                secondPrompt.AppendLine("{");
                secondPrompt.AppendLine("    public List<MealRecipes> DayMealPlans { get; set; }");
                secondPrompt.AppendLine("}");
                secondPrompt.AppendLine("public class DayMealPlans");
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
                secondPrompt.AppendLine("    public string Protein { get; set; }");
                secondPrompt.AppendLine("    public string Carbs { get; set; }");
                secondPrompt.AppendLine("    public string Fats { get; set; }");
                secondPrompt.AppendLine("    public string Calories { get; set; }");
                secondPrompt.AppendLine("}");
                secondPrompt.AppendLine("```\n");
                secondPrompt.AppendLine($"Please generate a JSON representation that follows the format of the `MealRecipes`,  each having {userInfos.MealFrequency} meals per day. The number of people for whom this week plan is intended is {userInfos.NumberOfPeople}. This information will be useful for determining the quantity of ingredients needed for each meal.");
                //secondPrompt.AppendLine($"Please make sure that the unit of measurement in the GroceryItem will be in the {userInfos.Unit}");
                secondPrompt.AppendLine("NOTE: Please generate the `MealRecipes` based on the following user preferences: \n\n" +
                "- `Objective`: " + userInfos.Objective + ". This should influence the total calories and macro distribution (proteins, carbohydrates, and fats) in the meal plan. For instance, if the objective is weight loss, aim for a caloric deficit. If it's muscle gain, aim for a caloric surplus with a higher protein count.\n\n" +
                "- `Weight`: " + userInfos.Weight + ". This is important to calculate the user's caloric needs.\n\n" +
                "- `Height`: " + userInfos.Height + ". This is used in calculating the user's Basal Metabolic Rate (BMR).\n\n" +
                "- `Age`: " + userInfos.Age + ". Age impacts metabolism, which should be factored into the caloric needs.\n\n" +
                "- `Gender`: " + userInfos.Gender + ". Men and women have different caloric needs, so adjust the meal plan accordingly.\n\n" +
                "- `MealFrequency`: " + userInfos.MealFrequency + ". The total calories and macros should be divided by the number of meals the user prefers to eat each day.\n\n" +
                "- `NumberOfPeople`: " + userInfos.NumberOfPeople + ". If more than one person will be eating the meals, adjust the ingredient quantities accordingly.\n\n" +
                "- `Unit`: " + userInfos.Unit + ". This refers to the unit of measurement preferred by the user. For certain ingredients, especially eggs, it is important to use countable units (like '1 egg', '2 eggs') instead of mass-based units (like 'grams'). Ensure that the eggs are represented as individual units, not in grams.\n\n" +
                "Also, consider the following dietary preferences and restrictions:\n\n" +
                "- `CuisineType`: " + userInfos.CuisineType + ". The meal recipes should follow the cuisine types preferred by the user.\n\n" +
                "- `Allergies`: " + userInfos.Allergies + ". Ensure that no allergens are included in the meal recipes.\n\n" +
                "- `CookingSkillLevel`: " + userInfos.CookingSkillLevel + ". The complexity of the recipes should match the user's cooking skill level.\n\n" +
                "- `PreferredIngredients`: " + userInfos.PreferredIngredients + ". Try to include these ingredients in the recipes.\n\n" +
                "- `DietaryPreferencesRestrictions`: " + userInfos.DietaryPreferencesRestrictions + ". Respect the user's dietary restrictions and preferences when generating meal recipes.\n\n" +
                "- `HealthConditions`: " + userInfos.HealthConditions + ". Some health conditions require dietary modifications, take this into consideration.\n\n" +
                "- `FoodDislikes`: " + userInfos.FoodDislikes + ". Avoid including these ingredients in the meal recipes.\n\n" +
                "Please ensure all these factors are properly reflected in the generated `MealMacros` and meal recipes.");
                secondPrompt.AppendLine("Please make sure the JSON follows the format of the `MealRecipes`, `GroceryItem`, `MealMacros` and `UserInfos` C# classes and is in a compact form with no unnecessary whitespace.");


                string prompt = secondPrompt.ToString();
                _logger.LogInformation("GenerateSecondPrompt: Successfully generated second prompt");
                _logger.LogDebug("GenerateSecondPrompt: Second prompt contents: {Prompt}", prompt);

                return prompt;
            }
            catch (Exception ex)
            {
                _logger.LogError("GenerateSecondPrompt: An error occurred while generating second prompt: {Message}", ex.Message);
                throw;
            }
        }


        private string GenerateSecondPrompt_PRO(UserInputDataModel userInfos, Meal meal)
        {
            StringBuilder secondPrompt = new StringBuilder();

            secondPrompt.AppendLine("You are a nutritionist assistant AI. Your task is to create a meal plan based on the user's preferences and nutritional needs. You have already been given the details of a meal. Now, you are to generate a JSON representation of a `MealRecipes` object based on this meal and the user's profile information.");

            secondPrompt.AppendLine($"The meal detail is as follows:\nMealType: {meal.MealType}\nMealName: {meal.MealName}\n");

            secondPrompt.AppendLine($"User Infos: Cuisine Type: {userInfos.CuisineType}, Age: {userInfos.Age}, Gender: {userInfos.Gender}, Weight: {userInfos.Weight}kg, Height: {userInfos.Height}cm, Objective: {userInfos.Objective}, Allergies: {userInfos.Allergies}, Cooking Skill Level: {userInfos.CookingSkillLevel}, Preferred ingredients: {string.Join(", ", userInfos.PreferredIngredients)}, Dietary Preferences/Restrictions: {userInfos.DietaryPreferencesRestrictions}, Health Conditions: {userInfos.HealthConditions}, Food Dislikes: {userInfos.FoodDislikes}, Preparation Time: {userInfos.PreparationTime}, Meal Frequency: {userInfos.MealFrequency}, Number Of People: {userInfos.NumberOfPeople}, Unit: {userInfos.Unit}, User Comments: {userInfos.UserComments}");

            secondPrompt.AppendLine("\nBased on these details, the `MealRecipes` object should include:");
            secondPrompt.AppendLine("1. A `MealName` which is the name of the meal.");
            secondPrompt.AppendLine("2. A `GroceryItems` list, where each item is an object containing an `IngredientName`, a `Quantity` in grams, and a `Unit` which is 'g' for grams.");
            secondPrompt.AppendLine("3. An `Instructions` list, which contains the step-by-step preparation instructions for the meal.");
            secondPrompt.AppendLine("4. The `MealMacros` which should include the `Protein`, `Carbs`, `Fats`, and `Calories` for the meal.");

            secondPrompt.AppendLine("\nHere are the corresponding C# classes:\n");
            secondPrompt.AppendLine("```csharp");
            secondPrompt.AppendLine("public class MealRecipes");
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
            secondPrompt.AppendLine("    public string Protein { get; set; }");
            secondPrompt.AppendLine("    public string Carbs { get; set; }");
            secondPrompt.AppendLine("    public string Fats { get; set; }");
            secondPrompt.AppendLine("    public string Calories { get; set; }");
            secondPrompt.AppendLine("}");
            secondPrompt.AppendLine("```\n");
            secondPrompt.AppendLine($"Please generate a JSON representation that follows the format of the `MealRecipes`,  each having {userInfos.MealFrequency} meals per day. The number of people for whom this week plan is intended is {userInfos.NumberOfPeople}. This information will be useful for determining the quantity of ingredients needed for each meal.");
            //secondPrompt.AppendLine($"Please make sure that the unit of measurement in the GroceryItem will be in the {userInfos.Unit}");
            secondPrompt.AppendLine("NOTE: Please generate the `MealRecipes` based on the following user preferences: \n\n" +
            "- `Objective`: " + userInfos.Objective + ". This should influence the total calories and macro distribution (proteins, carbohydrates, and fats) in the meal plan. For instance, if the objective is weight loss, aim for a caloric deficit. If it's muscle gain, aim for a caloric surplus with a higher protein count.\n\n" +
            "- `Weight`: " + userInfos.Weight + ". This is important to calculate the user's caloric needs.\n\n" +
            "- `Height`: " + userInfos.Height + ". This is used in calculating the user's Basal Metabolic Rate (BMR).\n\n" +
            "- `Age`: " + userInfos.Age + ". Age impacts metabolism, which should be factored into the caloric needs.\n\n" +
            "- `Gender`: " + userInfos.Gender + ". Men and women have different caloric needs, so adjust the meal plan accordingly.\n\n" +
            "- `MealFrequency`: " + userInfos.MealFrequency + ". The total calories and macros should be divided by the number of meals the user prefers to eat each day.\n\n" +
            "- `NumberOfPeople`: " + userInfos.NumberOfPeople + ". If more than one person will be eating the meals, adjust the ingredient quantities accordingly.\n\n" +
            "- `Unit`: " + userInfos.Unit + ". This refers to the unit of measurement preferred by the user, and should be considered while presenting the quantities of ingredients. For certain ingredients like eggs, consider using countable units (like '1 egg') instead of mass-based units (like 'grams').\n\n" +
            "Also, consider the following dietary preferences and restrictions:\n\n" +
            "- `CuisineType`: " + userInfos.CuisineType + ". The meal recipes should follow the cuisine types preferred by the user.\n\n" +
            "- `Allergies`: " + userInfos.Allergies + ". Ensure that no allergens are included in the meal recipes.\n\n" +
            "- `CookingSkillLevel`: " + userInfos.CookingSkillLevel + ". The complexity of the recipes should match the user's cooking skill level.\n\n" +
            "- `PreferredIngredients`: " + userInfos.PreferredIngredients + ". Try to include these ingredients in the recipes.\n\n" +
            "- `DietaryPreferencesRestrictions`: " + userInfos.DietaryPreferencesRestrictions + ". Respect the user's dietary restrictions and preferences when generating meal recipes.\n\n" +
            "- `HealthConditions`: " + userInfos.HealthConditions + ". Some health conditions require dietary modifications, take this into consideration.\n\n" +
            "- `FoodDislikes`: " + userInfos.FoodDislikes + ". Avoid including these ingredients in the meal recipes.\n\n" +
            "Please ensure all these factors are properly reflected in the generated `MealMacros` and meal recipes.");
            secondPrompt.AppendLine("Please make sure the JSON follows the format of the `MealRecipes`, `GroceryItem`, `MealMacros` and `UserInfos` C# classes and is in a compact form with no unnecessary whitespace.");


            return secondPrompt.ToString();
        }


        private async Task<string> GetSecondPromptResponse(string secondPrompt)
        {
            _logger.LogInformation("GetSecondPromptResponse: Starting to get response for second prompt");

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    APIAuthentication.Default = new APIAuthentication(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                    var aOpenAiAPI = new OpenAIAPI();
                    var aChat = CreateConversation(aOpenAiAPI);
                    aChat.AppendUserInput(secondPrompt);
                    var response = await aChat.GetResponseFromChatbotAsync();
                    _logger.LogInformation("GetSecondPromptResponse: Successfully got response for second prompt");
                    _logger.LogDebug("GetSecondPromptResponse: Second prompt response: {Response}", response);
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError("GetSecondPromptResponse: An error occurred while getting response for second prompt (attempt {Attempt}): {Message}", i + 1, ex.Message);
                    if (i == 9) throw;
                }
            }

            return null; // this line should not be reached, but is required for function to compile
        }



    }
}
