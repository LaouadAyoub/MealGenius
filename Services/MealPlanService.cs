using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Newtonsoft.Json;
using OpenAI_API;
using Stripe;
using System.Text;
// OpenAI_API.Models.Model.GPT4


namespace MealGeniusBackend.Services
{
    public interface IMealPlanService
    {
        Task GenerateMealPlan(UserTaskDTO userTaskDTO);
    }



    public class MealPlanService : IMealPlanService
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<MealPlanService> _logger;
        private readonly IOpenAIService _openAIService;
        private readonly IAzureBlobService _azureBlobService;
        public MealPlanService(UserDbContext userDbContext, ILogger<MealPlanService> logger, IOpenAIService openAIService, IAzureBlobService azureBlobService)
        {
            _dbContext = userDbContext;
            _logger = logger;
            _openAIService = openAIService;
            _azureBlobService = azureBlobService;
        }
        public async Task GenerateMealPlan(UserTaskDTO userTaskDTO)
        {
            var userTask = _dbContext.Tasks.Where(u => u.Id == userTaskDTO.Id).FirstOrDefault();

            try
            {
                // Logic for generating a user dashboard
                var userInput = _dbContext.UserInputs
                              .Where(u => u.TaskId == userTaskDTO.Id)
                              .FirstOrDefault();
                if (userInput is null || userInput.UserData is null)
                {
                    _logger.LogError("UserInput not found");
                    return;
                }

                var existingMealPlan = _dbContext.MealPlans.SingleOrDefault(mealPlan => mealPlan.TaskId == userTaskDTO.Id);

                if (existingMealPlan is not null)
                {
                    _logger.LogInformation("MealPlan already exists");
                    return;
                }

                // Generate First Json that contains the meals PreData
                string MealRecipeBreakdow_JsonExample = System.IO.File.ReadAllText("JsonFiles\\Meal_PreData_Generation.json");

                if (MealRecipeBreakdow_JsonExample is not null)
                { 
                    _logger.LogInformation($"MealRecipeBreakdow_JsonExample found \n {MealRecipeBreakdow_JsonExample}");
                }

                var UserInputsJson = userInput.UserData;
                string systemPromptJsonMealsGeneration = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                User information relevant informations is provided in the following JSON format.
                                {UserInputsJson}";

                string userPromptJsonMealsGeneration = $@"Create 12 personalized meal ideas, each with a unique blend of attributes. For every meal, include:

Meal Name: Craft a creative and inviting name.
Meal Type: Assign one or more categories (e.g., Breakfast, Lunch, Dinner, Snack, Dessert, Smoothie). A meal can belong to multiple types, like 'Breakfast' and 'Smoothie'.
Preparation Skill: Indicate the level of effort and skill required for the recipe, categorized into 'Easy', 'Intermediate', 'Advanced', and 'Expert'. 
- 'Easy' implies recipes that are quick and straightforward, requiring basic cooking skills and minimal ingredients. Ideal for beginners or those seeking a quick meal.
- 'Intermediate' involves recipes that require some cooking experience, introducing more complex techniques and a greater variety of ingredients, but still accessible to a motivated home cook.
- 'Advanced' denotes recipes that demand a good understanding of cooking techniques, involving multiple components, specialized ingredients, or equipment, suitable for those with significant cooking experience.
- 'Expert' represents the highest complexity level, requiring extensive culinary knowledge, precision, and patience, often including professional techniques and intricate presentations. 
Mood Suitability: Suggest one or more mood categories that will serve as tags for each meal. It's important to ensure that meals share common mood suitability tags to facilitate user exploration of related options. These tags should be imaginative and cater to various user preferences and emotional states (e.g., 'Adventure Seeker', 'Soul Soother', 'Memory Lane', 'Dreamy Indulgence'). By maintaining common mood tags across different meals, 
users can easily find meals that match their current mood or desired emotional state, enhancing personalization and user experience. Aim for a mix of moods that cater to a broad range of dietary preferences and emotional needs, ensuring a balanced and diverse meal selection, but ensure that meals share mood suitablity between each other

Note: Each filter category (Meal Type, Preparation Type, Mood Suitability) can have multiple options for a single meal to increase flexibility and user discoverability. For instance, a meal can be both a 'Breakfast' and a 'Smoothie', and suit moods like 'Energizing' and 'Refreshing'.
Ensure that the meals have common elements for users to explore related options and maintain a balance between creativity, dietary preferences, and culinary diversity.

Based on the user data provided, create a JSON structure with 12 personalized meals. Ensure each meal aligns with the user’s preferences, health goals, and lifestyle, reflecting the diversity and balance necessary for their diet.

Plz follow the format of the JSON file provided in the following line :

{MealRecipeBreakdow_JsonExample}";
                var userMealsJson = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptJsonMealsGeneration, userPromptJsonMealsGeneration, 1000, model: "gpt-4-1106-preview", temperature: 0.8);

                UserMealsRoot theUserMealsRoot = JsonConvert.DeserializeObject<UserMealsRoot>(userMealsJson);

                //Meal Recipe and PostData Generation
                var tasks = new List<Task>();
                foreach (var meal in theUserMealsRoot.UserMeals)
                {
                    tasks.Add(FillUserMealsData(meal, UserInputsJson));
                }
                await Task.WhenAll(tasks);


                string mealPlanJson = Newtonsoft.Json.JsonConvert.SerializeObject(theUserMealsRoot);



                // If no record exists, create a new one
                MealPlan newMealPlan = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    UserId = userTaskDTO.UserId,
                    TaskId = userTaskDTO.Id,
                    Title = "Sample Meal Plan",
                    MealPlanJson = mealPlanJson,
                    GroceryListJson = "",
                    CreatedAt = DateTime.UtcNow
                };

                // Add the new record to the database
                _dbContext.MealPlans.Add(newMealPlan);
                userTask.UserOutputStatus = UserOutputStatus.MealPlanCompleted;
                _dbContext.SaveChanges();
                _logger.LogInformation($"MealPlan generated for UserId: {userTaskDTO.UserId}");

                //Image Narrative Generation
                #region ImageGeneration
                //                string systemPromptImageGeneration = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.

                //                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                //                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                //                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                //                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                //                                User information relevant informations is provided in the following JSON format.
                //                                {UserInputsJson}"; 

                //                string userImageNarrativeGeneration = $@"
                //Based on the user infos and mood and from you creativity, Create a descriptive narrative for a set of meal images, defining a cohesive mood, style, and environment.
                //This narrative will be used as the basis for generating images with DALL·E 3 . The images should visually represent a variety of theUserMealsRoot in a consistent and appealing manner. Consider these aspects in your description:

                //Mood and Style: Describe the overall mood and artistic style of the images. Ensure that it's all about SIMPLICITY, and minimalistc style.

                //Environment Setting: Define the setting or backdrop for the theUserMealsRoot. Is it an outdoor picnic, a cozy home kitchen, an upscale restaurant, or a casual café?

                //Plate Presentation: Detail how the theUserMealsRoot should be presented on the plate. Should they be meticulously arranged, casually plated, or artistically styled?

                //Color Palette: Suggest a color palette that should be consistent across all images. Consider colors that evoke the mood and complement the food.

                //Additional Elements: Decide if there are any additional elements that should be included in every image, such as specific tableware, a particular type of garnish, or consistent lighting.

                //This narrative will guide the creation of a series of meal images that are visually harmonious and aligned with the defined mood and setting. The goal is to ensure that each image, while unique in its meal presentation, shares a common aesthetic thread with the others.

                //And remember : Food is not just to nourish the body

                //Food nourishes the soul 

                //You have to romance people

                //It has to be something that makes people go, omg, I can't wait to get a fork and dig into that
                //";

                //                var chatImageNarrativeGenerationResponse = await _openAIService.GetResponseAsync(systemPromptImageGeneration, userImageNarrativeGeneration, OpenAI_API.Models.Model.GPT4, 5000);

                //                // Image Generation
                //                foreach (var meal in theUserMealsRoot.UserMeals)
                //                {
                //                    string systemPrompt = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.

                //                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                //                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                //                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                //                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                //                                User information relevant informations is provided in the following JSON format.
                //                                {UserInputsJson}";

                //                    string userPromptImageGeneration = $@"
                //Generate a detailed prompt for DALL·E 3 to create an image of a meal. This image should be based on the provided descriptive narrative, ensuring consistency in mood, style, and environment. 
                //Additionally, incorporate specific details from the given recipe. 
                //The prompt should blend these elements to guide the creation of an image that represents the meal accurately while adhering to the overall aesthetic theme.

                //Please find the narrative below:
                //{chatImageNarrativeGenerationResponse}

                //Please find the recipe below:
                //{meal.Recipe}

                //Ensure the prompt doesnt exceed 3500 caracters.

                //And remember : Food is not just to nourish the body

                //Food nourishes the soul 

                //You have to romance people

                //It has to be something that makes people go, omg, I can't wait to get a fork and dig into that,

                //Ensure that the image should be simple and clear, with a focus on the meal itself, so that it can be easily understood by users. and not be distracting or confusing.
                //";

                //                    var chatImagePromptResponse = await _openAIService.GetResponseAsync(systemPrompt, userPromptImageGeneration, OpenAI_API.Models.Model.GPT4, 5000);


                //                    var imageUrl = await _openAIService.GenerateImageForMealAsync(chatImagePromptResponse);

                //                    if (!string.IsNullOrEmpty(imageUrl))
                //                    {
                //                        //updload the image on azure blob storage
                //                        var imageBlobUrl = await _azureBlobService.UploadImageAsync(imageUrl);
                //                        meal.MealImage = imageBlobUrl;
                //                    }                  
                //                    //if (!string.IsNullOrEmpty(imageUrl))
                //                    //{
                //                    //    string localImagePath = await DownloadAndSaveImage(imageUrl, meal.MealName);
                //                    //    meal.MealImage = localImagePath;
                //                    //}
                //                }
                //                string mealPlanJson_images = Newtonsoft.Json.JsonConvert.SerializeObject(theUserMealsRoot);

                //                if (existingMealPlan != null)
                //                {
                //                    existingMealPlan.MealPlanJson = mealPlanJson_images;
                //                    _dbContext.SaveChanges();
                //                    _logger.LogInformation("MealPlan updated with images successfully");
                //                }
                //                else
                //                {
                //                    var recentlySavedMealPlan = _dbContext.MealPlans.SingleOrDefault(mealPlan => mealPlan.TaskId == userTaskDTO.Id);
                //                    recentlySavedMealPlan.MealPlanJson = mealPlanJson_images;
                //                    _dbContext.SaveChanges();
                //                    _logger.LogInformation("MealPlan updated with images successfully");
                //                }
                //           
                #endregion
            }

            catch (Exception ex)
            {
                userTask.Status = UserTaskStatus.Failed;
                await _dbContext.SaveChangesAsync();
                _logger.LogError(ex, "Error while generating MealPlan");
            }
        }
        async Task FillUserMealsData(aMeal meal, string UserInputsJson)
        {
            meal.Tags = new List<string>();

            meal.Tags.AddRange(meal.MealType);
            meal.Tags.AddRange(meal.MoodSuitability);
            meal.Tags.Add(meal.PreparationSkill);

            meal.Recipe = await GetMealRecipe(meal, UserInputsJson);


            string systemPromptJsonMealDataGeneration = @"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.";

            var mealTotalBreakDown_JsonExamplePath = "JsonFiles/MealTotalBreakDown_JsonExample.json";
            string mealTotalBreakDown_JsonExample = System.IO.File.ReadAllText(mealTotalBreakDown_JsonExamplePath);
            string userPromptJsonMealBreakdown = $@"
The following is the recipe of the user
[Recipe : {meal.Recipe}]

to make this recipe easy to parse by our C# backend and React app int the front we want to serialize it to a json, can you create this json with the following format :

[JsonFormat : {mealTotalBreakDown_JsonExample} ]

In this json The MealDetails object will provide a quick visual representation of key information as tags and assist in generating grocery lists. 
Meal Name: Specify the name of the dish.

Macronutrient: List each macronutrient (proteins, carbohydrates, fats, and calories) with their respective quantities in grams (g) or calories (kcal), please provide one average value for calories if there is a range.
    In this section it's very important and crucial that you provide the exact value of each macronutrient in the meal and follow this exact format, which will help up to parse the data and display it in the front end app.
    So Please follow this format :
    ""Macronutrients"": {{
      ""Proteins"": ""40g"" , //here it's important that that format will be 40g ,a number then g
      ""Carbohydrates"": ""75g"", //here it's important that that format will be 75g, a number then g
      ""Fats"": ""35g"", //here it's important that that format will be 35g, a number then g
      ""Calories"": ""800kcal""// here it's important that that format will be 800kcal, a number then kcal, if there is a range like 250-450kcal provide the average value that would be 350kcal
    }},


Micronutrient: Provide a simple list of key micronutrients present in the meal, formatted as tags. Include only the names of these nutrients.

Serving Size: Add a tag for the serving size, indicating the quantity in grams of the final prepared dish.

Ingredient List for Groceries: List all the ingredients used in the recipe, without specifying quantities. This list will be used for generating grocery lists. add items even if they are optional.

and to make easier to parse and style meal recipes in the react app MealInformation object should contain the exact recipe of 
the meal but decomposed into elements.
	

This json representation will help us to divide the markdown recipe into its components while keeping exactly the same content in markdown format, 
write the content of each section without rewriting the title of the section plz
";

            var userMealDataBreakdownJson = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptJsonMealDataGeneration, userPromptJsonMealBreakdown, 4096);
            var MealDataBreakdownJson = JsonConvert.DeserializeObject<MealTotalBreakdown>(userMealDataBreakdownJson);


            meal.mealRecipeBreakdown = MealDataBreakdownJson!.MealInformation;
            meal.Macronutrients = MealDataBreakdownJson.MealDetails.Macronutrients;
            meal.Micronutrients = MealDataBreakdownJson.MealDetails.Micronutrients;
            meal.ServingSize = MealDataBreakdownJson.MealDetails.ServingSize;
            meal.Ingredients = MealDataBreakdownJson.MealDetails.Ingredients;

            var macroPourcentage = CalculateMacronutrientPercentagesFromString(meal.Macronutrients.Calories, meal.Macronutrients.Carbohydrates, meal.Macronutrients.Proteins, meal.Macronutrients.Fats);

            meal.Macronutrients_Pourcentage = new Macronutrients_Pourcentage
            {
                CarbsPercentage = macroPourcentage.CarbsPercentage,
                ProteinPercentage = macroPourcentage.ProteinPercentage,
                FatsPercentage = macroPourcentage.FatsPercentage
            };
        }

        public (string CarbsPercentage, string ProteinPercentage, string FatsPercentage) CalculateMacronutrientPercentagesFromString(
            string totalCaloriesStr, string carbsStr, string proteinStr, string fatsStr)
        {
            try
            {
                // Convert string values to double, extracting the numeric part
                double totalCalories = double.Parse(totalCaloriesStr.TrimEnd('k', 'c', 'a', 'l').Trim());
                double carbsGrams = double.Parse(carbsStr.TrimEnd('g').Trim());
                double proteinGrams = double.Parse(proteinStr.TrimEnd('g').Trim());
                double fatsGrams = double.Parse(fatsStr.TrimEnd('g').Trim());

                // Calories per gram for each macronutrient
                const double caloriesPerGramCarbs = 4.0;
                const double caloriesPerGramProtein = 4.0;
                const double caloriesPerGramFats = 9.0;

                // Calculate total calories from each macronutrient
                double caloriesFromCarbs = carbsGrams * caloriesPerGramCarbs;
                double caloriesFromProtein = proteinGrams * caloriesPerGramProtein;
                double caloriesFromFats = fatsGrams * caloriesPerGramFats;

                // Calculate the percentage of total calories for each macronutrient
                double carbsPercentage = (caloriesFromCarbs / totalCalories) * 100;
                double proteinPercentage = (caloriesFromProtein / totalCalories) * 100;
                double fatsPercentage = (caloriesFromFats / totalCalories) * 100;

                // Format and return the percentages as strings with two decimal places
                return ($"{carbsPercentage:F2}", $"{proteinPercentage:F2}", $"{fatsPercentage:F2}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating macronutrient percentages: {ex.Message}");
                // make a quick prompt to fix it
                //throw new Exception("Error calculating macronutrient percentages");
                return ("Error", "Error", "Error");
            }
        }

        private async Task<string> GetMealRecipe(aMeal meal, string UserInputsJson)
        {

            string systemPrompt = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                User information relevant informations is provided in the following JSON format.
                                {UserInputsJson}";

            string userRecipeGenerationprompt = $@"Create a personalized and engaging recipe for this meal
{meal.ToString()}
Ensure the recipe is tailored to the user, with a friendly, chef-like tone, incorporating these specific elements:

## Introduction
	Begin with a warm, personalized greeting, mentioning the user's name and introducing the recipe.
	Recipe Name and Description: Clearly state the recipe name and provide an engaging description, highlighting why this meal is beneficial for the user.

## Ingredients
	Detail the ingredients for one serving  with easy-to-follow quantities. (specify the serving size in the end explicitly based on the ingredients),

## Serving Size
	based on the quantities of the ingredients Specify the serving size in grams for the final prepared dish, making it clear that this refers to the ready-to-serve meal.

## Detailed Cooking Instructions
	specify each step, followed by a detailed, easy-to-follow explanation. Make the instructions engaging and motivating, encouraging users to enjoy the cooking process.
	please follow this format : 
		an clear introduction of this section
		**Step 1:** detailed, easy-to-follow explanation for step 1
		**Step x:** detailed, easy-to-follow explanation for step x, the more steps, the better
		same for other steps...

## Appliances/Tools Needed
	based on the cooking instructions List the kitchen appliances and tools required to prepare the meal, ensuring users are well-prepared before starting.

## Meal Timing Recommendations
	Include advice on the best time of day to enjoy this meal, and when it might be less ideal, based on its nutritional content.

## Macronutrients  Breakdown
	At the end of the recipe, provide a detailed breakdown of macronutrients (calories, carbohydrates, proteins, fats) This is placed last to ensure the language model has full context of the ingredients, quantities, and serving size for more accurate calculations.
	Calories: Total energy content of the meal, do not give a range specify an average of the total calories based on the ingredients, crucial for managing energy intake.
	Carbohydrates: calculate the quantity in grams, includes the types (e.g., simple vs. complex) 
	Proteins: calculate the quantity in grams
	Fats: calculate the quantity in grams, Describes the types of fats present (saturated, unsaturated).
	
## Key micronutrients
	Emphasizing each micronutrient(vitamins & minerals) and Fibers in the meal and in which ingredients it's presents and its health benefits and relevance to the user.

## Health Benefits
	This section offers a holistic view of the meal's nutritional profile, combining information on macronutrients, key micronutrients, and fibers. It showcases how each component contributes to the user's health and supports their health goals.

## Conclusion
	Conclude with a message that reinforces how this meal contributes to the user's health and enjoyment.

it's imporant to respect the format of the markdown that we specified for you SPECIALLY THE HEADERS ##, so it'll be easier for us to parse by our app

Markdown Format: Use Markdown for clear formatting, emphasizing important sections in bold.

Note for GPT : Important data and conclusions should be bolded for emphasis

Your goal is to create a recipe that is nutritionally informative, fun and easy to follow, and resonates personally with the user, inspiring them to confidently prepare and enjoy the meal.

Food is not just to nourish the body, Food nourishes the soul, You have to romance people, It has to be something that makes people go, omg, I can't wait to get a fork and dig into that

Use a conversational tone, as a chef is speaking directly to them, starting by greeting the user by their name.

Note for GPT : PLZ DIRECTLY START BY GENERATING THE MARKDOWN, !!!DO NOT write something before like ""---"" or ""```markdown""

Start directly by the markdown header  : # The Title of the Recipe
";

            var chatRecipeResponse = await _openAIService.GetResponseAsync(systemPrompt, userRecipeGenerationprompt, OpenAI_API.Models.Model.GPT4_Turbo, 4095);

            return chatRecipeResponse;
        }

        #region old unused code
        static async Task<string> DownloadAndSaveImage(string imageUrl, string imageName)
        {
            string directoryPath = @"C:\persoProjects\MealPlanner\MealgeniusFull\MealGenius_ui\mealsImages\newUserImages";

            string sanitizedImageName = SanitizeFileName(imageName);
            string localFilePath = Path.Combine(directoryPath, sanitizedImageName + ".png");

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await System.IO.File.WriteAllBytesAsync(localFilePath, imageBytes);
                    return localFilePath; // Return the local file path
                }
            }

            return null; // Return null if download fails
        }
        // Method to sanitize file names
        static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_'); // Replace invalid chars with underscore
            }
            return fileName;
        }
        #endregion
    }

    #region Models
    public class MealTotalBreakdown
    {
        public MealData MealDetails;
        public MealRecipeBreakdown MealInformation;
    }


    public class MealData
    {
        public string MealName { get; set; }
        public Macronutrients Macronutrients { get; set; }
        public List<string> Micronutrients { get; set; }
        public string ServingSize { get; set; }
        public List<string> Ingredients { get; set; }
    }

    public class MealRecipeBreakdown
    {
        public string NameOfTheMeal { get; set; }
        public string Introduction { get; set; }
        public string Ingredients { get; set; }
        public string ServingText { get; set; }
        public List<string> DetailedCookingInstructions { get; set; }
        public string AppliancesAndTools { get; set; }
        public string MealTimingRecommendations { get; set; }
        public string Macronutrients_Section { get; set; }
        public string Key_Micronutrients { get; set; }
        public string Health_Benefits { get; set; }
        public string Conclusion { get; set; }
    }
    public class Macronutrients
    {
        public string Proteins { get; set; }
        public string Carbohydrates { get; set; }
        public string Fats { get; set; }
        public string Calories { get; set; }

        public override string ToString()
        {
            return $"Proteins: {Proteins}, Carbohydrates: {Carbohydrates}, Fats: {Fats}, Calories: {Calories}";
        }
    }

    public class Macronutrients_Pourcentage
    {
        public string ProteinPercentage { get; set; }
        public string CarbsPercentage { get; set; }
        public string FatsPercentage { get; set; }
    }
    public class aMeal
    {
        public string MealName { get; set; }
        public List<string> MealType { get; set; }
        public string PreparationSkill { get; set; }
        public List<string> MoodSuitability { get; set; }
        public List<string> Tags { get; set; }

        public Macronutrients Macronutrients { get; set; }
        
        public Macronutrients_Pourcentage Macronutrients_Pourcentage { get; set; }
        public List<string> Micronutrients { get; set; }
        public string ServingSize { get; set; }
        public List<string> Ingredients { get; set; }


        public string Recipe { get; set; }  // <-- New property

        public MealRecipeBreakdown mealRecipeBreakdown { get; set; }
        public string MealImage { get; set; }

        public override string ToString()
        {
            var stringBuilder = new System.Text.StringBuilder();

            stringBuilder.AppendLine($"Meal Name: {MealName}");

            if (MealType != null && MealType.Any())
                stringBuilder.AppendLine($"Meal Type: {string.Join(", ", MealType)}");

            if (PreparationSkill != null && PreparationSkill.Any())
                stringBuilder.AppendLine($"Preparation Skill: {string.Join(", ", PreparationSkill)}");

            if (MoodSuitability != null && MoodSuitability.Any())
                stringBuilder.AppendLine($"Mood Suitability: {string.Join(", ", MoodSuitability)}");

            if (Macronutrients != null)
                stringBuilder.AppendLine($"Macronutrients: {Macronutrients}");

            if (Micronutrients != null && Micronutrients.Any())
                stringBuilder.AppendLine($"Micronutrients: {string.Join(", ", Micronutrients)}");

            if (!string.IsNullOrWhiteSpace(ServingSize))
                stringBuilder.AppendLine($"Serving Size: {ServingSize}");

            if (Ingredients != null && Ingredients.Any())
                stringBuilder.AppendLine($"Ingredients: {string.Join(", ", Ingredients)}");

            return stringBuilder.ToString();
        }
    }

    public class UserMealsRoot
    {
        public List<aMeal> UserMeals { get; set; }
        public List<string> GetAllIngredients()
        {
            List<string> allIngredients = new List<string>();

            foreach (aMeal meal in UserMeals)
            {
                allIngredients.AddRange(meal.Ingredients);
            }

            return allIngredients;
        }

        public string DisplayAllIngredients()
        {
            List<string> allIngredients = GetAllIngredients();
            var stringBuilder = new StringBuilder();

            foreach (string ingredient in allIngredients)
            {
                stringBuilder.AppendLine($"- {ingredient}");
            }

            return stringBuilder.ToString();
        }
    }
    #endregion
}