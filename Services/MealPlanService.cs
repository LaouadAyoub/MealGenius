using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models.Model_UI;
using MealGeniusBackend.Models.ModelGPT;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using static MealGeniusBackend.Controllers.MainAPIController;
using System.Text.Json;
using System.Net.Http.Json;
using OpenAI_API.Images;

namespace MealGeniusBackend.Services
{
    public interface IMealPlanService
    {
        Task GenerateMealPlan(UserTaskDTO userTaskDTO);
    }



    public class MealPlanService : IMealPlanService
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<UserDashboardService> _logger;
        private readonly OpenAIAPI _openAiApi;


        public MealPlanService(UserDbContext userDbContext, ILogger<UserDashboardService> logger, OpenAIAPI openAIAPI)
        {
            _dbContext = userDbContext;
            _logger = logger;
            _openAiApi = openAIAPI;
        }
        public async Task GenerateMealPlan(UserTaskDTO userTaskDTO)
        {
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

                // Generate First Json that contains the meals PreData
                string systemPromptJsonMealsGeneration = @"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.
User information relevant informations is provided in the following JSON format.
{
  ""userDetails"": {
    ""name"": ""Ayoub"",
    ""age"": 25,
    ""gender"": ""Male"",
    ""weight"": ""68 kg"",
    ""height"": ""192 cm"",
    ""activityLevel"": ""Regularly Active"",
    ""foodAllergies"": [],
    ""additionalAllergyNotes"": """",
    ""healthConditions"": [""None""],
    ""additionalHealthNotes"": """"
  },
  ""nutritionalGoals"": {
    ""primaryGoal"": ""Weight Gain and build strong BIG muscles"",
    ""secondaryGoals"": [""Build BIG strong muscles"",""Energy and Stamina Enhancement"", ""Digestive Health"", ""Mental Wellness and Focus""],
    ""nutritionKnowledgeLevel"": ""Beginner"",
    ""weightManagementSpecifics"": ""Fast Weight Gain"",
    ""goalWeight"": ""75 kg"",
    ""dietaryPreferences"": {
      ""foodSource"": [""None Specified""],
      ""macronutrientFocus"": [""Let our AI choose based on your nutritional goals (Recommended)""]
    },
    ""managingHealthConditions"": [""None""],
    ""managingMentalHealthConditions"": [""None""],
    ""additionalNutritionalInformation"": """"
  },
  ""mealPlanPreferences"": {
    ""cookingSkillLevel"": ""Some Experience"",
    ""mealSizePreference"": ""Larger Main Meals"",
    ""favoriteCuisines"": [""Not Specified""],
    ""favoriteDishCategories"": [""Pasta Dishes"", ""Grilled Foods"", ""Stir-Fries""],
    ""groceryListPreferences"": {
      ""shoppingStyle"": ""Balanced"",
      ""stapleItems"": [""Not Specified""],
      ""dislikedIngredients"": [],
      ""idealCookingTime"": ""Moderately Involved"",
      ""kitchenAppliances"": [""Stove/Oven"", ""Blender""]
    },
    ""snackingHabits"": ""Occasionally"",
    ""supplementUse"": [""Protein Powders""],
    ""budgetConstraints"": ""Moderate Budget""
  }
}";
                string userPromptJsonMealsGeneration = @"Create 12 personalized meal ideas, each with a unique blend of attributes. For every meal, include:

Meal Name: Craft a creative and inviting name.
Meal Type: Assign one or more categories (e.g., Breakfast, Lunch, Dinner, Snack, Dessert, Smoothie). A meal can belong to multiple types, like 'Breakfast' and 'Smoothie'.
Preparation Type: Describe the preparation effort with multiple possibilities (e.g., 'Quick Fix', 'Leisurely Cooking', 'No-Cook Delight', 'Weekend Project').
Mood Suitability: Suggest one or more imaginative mood categories (e.g., 'Adventure Seeker', 'Soul Soother', 'Memory Lane', 'Dreamy Indulgence').
Note: Each filter category (Meal Type, Preparation Type, Mood Suitability) can have multiple options for a single meal to increase flexibility and user discoverability. For instance, a meal can be both a 'Breakfast' and a 'Smoothie', and suit moods like 'Energizing' and 'Refreshing'.

Ensure that the theUserMealsRoot have common elements for users to explore related options and maintain a balance between creativity, dietary preferences, and culinary diversity.

Based on the user data provided, create a JSON structure with 12 personalized theUserMealsRoot. Ensure each meal aligns with the user’s preferences, health goals, and lifestyle, reflecting the diversity and balance necessary for their diet.

Plz follow the format of the JSON file provided in the following line :

{
  ""UserMeals"": [ // Array of meal objects
    {
      ""MealName"": ""Protein Power Pasta"", // Name of the meal
      ""MealType"": [""Lunch"", ""Dinner""], // Categories of the meal (can be multiple)
      ""PreparationType"": [""Quick Fix""], // Preparation effort level (can be multiple)
      ""MoodSuitability"": [""Adventure Seeker"", ""Muscle Builder""] // Moods suitable for the meal (can be multiple)
    },
    // ... other meal objects
    {
      ""MealName"": ""Energizing Egg Stir-fry"", // Another meal example
      ""MealType"": [""Breakfast"", ""Dinner""], // This meal fits both breakfast and dinner categories
      ""PreparationType"": [""Quick Fix""], // Indicates a quick preparation time
      ""MoodSuitability"": [""Morning Boost"", ""Muscle Builder""] // Suitable for a morning energy boost and muscle building
    },
    // ... continue with other theUserMealsRoot
    {
      ""MealName"": ""Tummy-Friendly Tuna Toasts"", // Last meal example
      ""MealType"": [""Breakfast"", ""Lunch""], // Suitable for breakfast and lunch
      ""PreparationType"": [""No-Cook Delight""], // No cooking required, easy to prepare
      ""MoodSuitability"": [""Soul Soother"", ""Energizing""] // Soothing yet energizing meal
    }
  ]
}

";
                var userMealsJson = GenerateJsonBasedOnPromptResponse(_openAiApi, systemPromptJsonMealsGeneration, userPromptJsonMealsGeneration);

                UserMealsRoot theUserMealsRoot = JsonConvert.DeserializeObject<UserMealsRoot>(userMealsJson);



                //Meal Recipe and PostData Generation
                var tasks = new List<Task>();
                foreach (var meal in theUserMealsRoot.UserMeals)
                {
                    tasks.Add(FillUserMealsData(meal));

                }
                await Task.WhenAll(tasks);



                //GroceryList Generation
                var chatGroceryListPromptGeneration = CreateConversationGPT4_6000token(_openAiApi);
                var systemPromptGroceryListGeneration = @"
You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

User information relevant informations is provided in the following JSON format.
{
  ""userDetails"": {
    ""name"": ""Ayoub"",
    ""age"": 25,
    ""gender"": ""Male"",
    ""weight"": ""68 kg"",
    ""height"": ""192 cm"",
    ""activityLevel"": ""Regularly Active"",
    ""foodAllergies"": [],
    ""additionalAllergyNotes"": """",
    ""healthConditions"": [""None""],
    ""additionalHealthNotes"": """"
  },
  ""nutritionalGoals"": {
    ""primaryGoal"": ""Weight Gain and build strong BIG muscles"",
    ""secondaryGoals"": [""Build BIG strong muscles"",""Energy and Stamina Enhancement"", ""Digestive Health"", ""Mental Wellness and Focus""],
    ""nutritionKnowledgeLevel"": ""Beginner"",
    ""weightManagementSpecifics"": ""Fast Weight Gain"",
    ""goalWeight"": ""75 kg"",
    ""dietaryPreferences"": {
      ""foodSource"": [""None Specified""],
      ""macronutrientFocus"": [""Let our AI choose based on your nutritional goals (Recommended)""]
    },
    ""managingHealthConditions"": [""None""],
    ""managingMentalHealthConditions"": [""None""],
    ""additionalNutritionalInformation"": """"
  },
  ""mealPlanPreferences"": {
    ""cookingSkillLevel"": ""Some Experience"",
    ""mealSizePreference"": ""Larger Main Meals"",
    ""favoriteCuisines"": [""Not Specified""],
    ""favoriteDishCategories"": [""Pasta Dishes"", ""Grilled Foods"", ""Stir-Fries""],
    ""groceryListPreferences"": {
      ""shoppingStyle"": ""Balanced"",
      ""stapleItems"": [""Not Specified""],
      ""dislikedIngredients"": [],
      ""idealCookingTime"": ""Moderately Involved"",
      ""kitchenAppliances"": [""Stove/Oven"", ""Blender""]
    },
    ""snackingHabits"": ""Occasionally"",
    ""supplementUse"": [""Protein Powders""],
    ""budgetConstraints"": ""Moderate Budget""
  }
}
";
                string userPromptGroceryListGeneration = $@"
Generate a markdown text that provides a comprehensive guide on their grocery list, tailored to their personal data and health goals. 


Create a grocery list for the user based on a provided [list of ingredients] The list should be organized by categories and follow the artistic direction of MealGenius, making it educational and enjoyable. 

Grocery List Introduction:
   Markdown Header level 1: # Your Personalized Grocery Adventure
   Content: Begin with a fun and welcoming introduction to the grocery list, setting the tone for a nutritional journey.
   Organize Ingredients by Category:

Organize Ingredients by Category:
   Markdown Header level 1: # Navigating Your Grocery Categories
   Content: 
      List the ingredients, organizing them into categories such as 'Fruits & Vegetables', 'Proteins', 'Dairy', 'Grains', etc. You should be creative with category names based on the user's data. 
      Each category will be in a Markdown Header level 2.
      Each category will have a list of ingredients, each ingredient will be in a Markdown Header level 3.
      Each Ingredient will have 2 bullet points linked to it :
         - Benefits : Explain how this ingredient contributes to health and well-being and specilly to the user's goal.
         - Nutrients : Highlight the key nutrients found in the ingredient.

Make each description both informative and entertaining, aligning with the MealGenius theme of making nutrition education enjoyable.

- User Data is provided in the system prompt
  [list of ingredients : this list contains the list of ingredients that the user needs to prepare all their meals :
{theUserMealsRoot.DisplayAllIngredients()}


                ]

Sum up all these ingredients to generate a detailed grocery list that follows the provided format
  ]
You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.
Use a conversational tone, as science popularizer is speaking directly to them, starting by greeting the user by their name.
!! DIRECTLY START BY GENERATING THE MARKDOWN don't write text before like ""---""
Note for GPT : Of course, if there is an ingredient that is specified twice, you don't need to write it twice, just write it once please.
Note for GPT :  Generate the entire Grocery list, don't put comments like … (continuation), where you let the user continue from his head, just write the entire complete grocery list please, Mention ALL the ingredients that i provided to you please !!!!
";
                chatGroceryListPromptGeneration.AppendSystemMessage(systemPromptGroceryListGeneration);
                chatGroceryListPromptGeneration.AppendUserInput(userPromptGroceryListGeneration);
                var chatGroceryListGenerationResponse = await chatGroceryListPromptGeneration.GetResponseFromChatbotAsync();


                //Image Narrative Generation
                var chatImageNarrativeGeneration = CreateConversationGPT4(_openAiApi);
                string systemPromptImageGeneration = @"
You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

User information relevant informations is provided in the following JSON format.
{
  ""userDetails"": {
    ""name"": ""Ayoub"",
    ""age"": 25,
    ""gender"": ""Male"",
    ""weight"": ""68 kg"",
    ""height"": ""192 cm"",
    ""activityLevel"": ""Regularly Active"",
    ""foodAllergies"": [],
    ""additionalAllergyNotes"": """",
    ""healthConditions"": [""None""],
    ""additionalHealthNotes"": """"
  },
  ""nutritionalGoals"": {
    ""primaryGoal"": ""Weight Gain and build strong BIG muscles"",
    ""secondaryGoals"": [""Build BIG strong muscles"",""Energy and Stamina Enhancement"", ""Digestive Health"", ""Mental Wellness and Focus""],
    ""nutritionKnowledgeLevel"": ""Beginner"",
    ""weightManagementSpecifics"": ""Fast Weight Gain"",
    ""goalWeight"": ""75 kg"",
    ""dietaryPreferences"": {
      ""foodSource"": [""None Specified""],
      ""macronutrientFocus"": [""Let our AI choose based on your nutritional goals (Recommended)""]
    },
    ""managingHealthConditions"": [""None""],
    ""managingMentalHealthConditions"": [""None""],
    ""additionalNutritionalInformation"": """"
  },
  ""mealPlanPreferences"": {
    ""cookingSkillLevel"": ""Some Experience"",
    ""mealSizePreference"": ""Larger Main Meals"",
    ""favoriteCuisines"": [""Not Specified""],
    ""favoriteDishCategories"": [""Pasta Dishes"", ""Grilled Foods"", ""Stir-Fries""],
    ""groceryListPreferences"": {
      ""shoppingStyle"": ""Balanced"",
      ""stapleItems"": [""Not Specified""],
      ""dislikedIngredients"": [],
      ""idealCookingTime"": ""Moderately Involved"",
      ""kitchenAppliances"": [""Stove/Oven"", ""Blender""]
    },
    ""snackingHabits"": ""Occasionally"",
    ""supplementUse"": [""Protein Powders""],
    ""budgetConstraints"": ""Moderate Budget""
  }
}

";
                string userImageNarrativeGeneration = $@"
Based on the user infos and mood and from you creativity, Create a descriptive narrative for a set of meal images, defining a cohesive mood, style, and environment.
This narrative will be used as the basis for generating images with DALL·E 3 . The images should visually represent a variety of theUserMealsRoot in a consistent and appealing manner. Consider these aspects in your description:

Mood and Style: Describe the overall mood and artistic style of the images. Ensure that it's all about SIMPLICITY, and minimalistc style.

Environment Setting: Define the setting or backdrop for the theUserMealsRoot. Is it an outdoor picnic, a cozy home kitchen, an upscale restaurant, or a casual café?

Plate Presentation: Detail how the theUserMealsRoot should be presented on the plate. Should they be meticulously arranged, casually plated, or artistically styled?

Color Palette: Suggest a color palette that should be consistent across all images. Consider colors that evoke the mood and complement the food.

Additional Elements: Decide if there are any additional elements that should be included in every image, such as specific tableware, a particular type of garnish, or consistent lighting.

This narrative will guide the creation of a series of meal images that are visually harmonious and aligned with the defined mood and setting. The goal is to ensure that each image, while unique in its meal presentation, shares a common aesthetic thread with the others.

And remember : Food is not just to nourish the body

Food nourishes the soul 

You have to romance people

It has to be something that makes people go, omg, I can't wait to get a fork and dig into that
";
                chatImageNarrativeGeneration.AppendSystemMessage(systemPromptImageGeneration);
                chatImageNarrativeGeneration.AppendUserInput(userImageNarrativeGeneration);
                var chatImageNarrativeGenerationResponse = await chatImageNarrativeGeneration.GetResponseFromChatbotAsync();

                // Image Generation
                foreach (var meal in theUserMealsRoot.UserMeals)
                {
                    var chatImagePromptGeneration = CreateConversationGPT4(_openAiApi);

                    string systemPrompt = @"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                User information relevant informations is provided in the following JSON format.
                                {
                                    ""userDetails"": {
                                    ""name"": ""Ayoub"",
                                    ""age"": 25,
                                    ""gender"": ""Male"",
                                    ""weight"": ""68 kg"",
                                    ""height"": ""192 cm"",
                                    ""activityLevel"": ""Regularly Active"",
                                    ""foodAllergies"": [],
                                    ""additionalAllergyNotes"": """",
                                    ""healthConditions"": [""None""],
                                    ""additionalHealthNotes"": """"
                                    },
                                    ""nutritionalGoals"": {
                                    ""primaryGoal"": ""Weight Gain and build strong BIG muscles"",
                                    ""secondaryGoals"": [""Build BIG strong muscles"",""Energy and Stamina Enhancement"", ""Digestive Health"", ""Mental Wellness and Focus""],
                                    ""nutritionKnowledgeLevel"": ""Beginner"",
                                    ""weightManagementSpecifics"": ""Fast Weight Gain"",
                                    ""goalWeight"": ""75 kg"",
                                    ""dietaryPreferences"": {
                                        ""foodSource"": [""None Specified""],
                                        ""macronutrientFocus"": [""Let our AI choose based on your nutritional goals (Recommended)""]
                                    },
                                    ""managingHealthConditions"": [""None""],
                                    ""managingMentalHealthConditions"": [""None""],
                                    ""additionalNutritionalInformation"": """"
                                    },
                                    ""mealPlanPreferences"": {
                                    ""cookingSkillLevel"": ""Some Experience"",
                                    ""mealSizePreference"": ""Larger Main Meals"",
                                    ""favoriteCuisines"": [""Not Specified""],
                                    ""favoriteDishCategories"": [""Pasta Dishes"", ""Grilled Foods"", ""Stir-Fries""],
                                    ""groceryListPreferences"": {
                                        ""shoppingStyle"": ""Balanced"",
                                        ""stapleItems"": [""Not Specified""],
                                        ""dislikedIngredients"": [],
                                        ""idealCookingTime"": ""Moderately Involved"",
                                        ""kitchenAppliances"": [""Stove/Oven"", ""Blender""]
                                    },
                                    ""snackingHabits"": ""Occasionally"",
                                    ""supplementUse"": [""Protein Powders""],
                                    ""budgetConstraints"": ""Moderate Budget""
                                    }
                                }";

                    string userPromptImageGeneration = $@"
Generate a detailed prompt for DALL·E 3 to create an image of a meal. This image should be based on the provided descriptive narrative, ensuring consistency in mood, style, and environment. 
Additionally, incorporate specific details from the given recipe. 
The prompt should blend these elements to guide the creation of an image that represents the meal accurately while adhering to the overall aesthetic theme.

Please find the narrative below:
{chatImageNarrativeGenerationResponse}

Please find the recipe below:
{meal.Recipe}

Ensure the prompt doesnt exceed 3500 caracters.

And remember : Food is not just to nourish the body

Food nourishes the soul 

You have to romance people

It has to be something that makes people go, omg, I can't wait to get a fork and dig into that,

Ensure that the image should be simple and clear, with a focus on the meal itself, so that it can be easily understood by users. and not be distracting or confusing.
";

                    chatImagePromptGeneration.AppendSystemMessage(systemPrompt);
                    chatImagePromptGeneration.AppendUserInput(userPromptImageGeneration);

                    var chatImagePromptResponse = await chatImagePromptGeneration.GetResponseFromChatbotAsync();

                    bool isImageCreated = false;
                    int retryCount = 0;
                    const int maxRetries = 10;

                    while (!isImageCreated && retryCount < maxRetries)
                    {
                        try
                        {
                            _logger.LogInformation($"Attempt {retryCount + 1} to generate image for meal '{meal.MealName}'.");


                            var result = await _openAiApi.ImageGenerations.CreateImageAsync(
                            new ImageGenerationRequest(chatImagePromptResponse, OpenAI_API.Models.Model.DALLE3, ImageSize._1024, "hd"));

                            var imageUrl = result.Data[0].Url;
                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                string localImagePath = await DownloadAndSaveImage(imageUrl, meal.MealName);
                                meal.MealImage = localImagePath;
                                isImageCreated = true;
                                _logger.LogInformation($"Image successfully generated and saved for meal '{meal.MealName}'.");

                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation($"Error during image generation for meal '{meal.MealName}': {ex.Message}");

                            retryCount++;
                            if (retryCount >= maxRetries)
                            {
                                _logger.LogInformation($"Max retry attempts reached for meal '{meal.MealName}'.");
                                throw; // Rethrow the exception if the max retries have been reached
                            }
                        }
                    }
                }

                string mealPlanJson = Newtonsoft.Json.JsonConvert.SerializeObject(theUserMealsRoot);




                var existingMealPlan = _dbContext.MealPlans.FirstOrDefault(mp => mp.TaskId == userTaskDTO.Id);
                if (existingMealPlan != null)
                {
                    // If a record exists, update it
                    existingMealPlan.MealPlanJson = mealPlanJson; // Assuming 'mealPlan' holds the updated meal plan
                    //existingMealPlan.GroceryListJson = groceryList; // Assuming 'groceryList' holds the updated grocery list
                    existingMealPlan.Title = "Updated Meal Plan Title"; // Update other fields as necessary
                    existingMealPlan.CreatedAt = DateTime.UtcNow;
                    existingMealPlan.GroceryListJson = chatGroceryListGenerationResponse; // Contains the grocery list of the week
                }
                else
                {
                    // If no record exists, create a new one
                    MealPlan newMealPlan = new MealPlan
                    {
                        Id = Guid.NewGuid(),
                        UserId = userTaskDTO.UserId,
                        TaskId = userTaskDTO.Id,
                        Title = "Sample Meal Plan",
                        MealPlanJson = mealPlanJson,
                        GroceryListJson = chatGroceryListGenerationResponse, // Contains the grocery list of the week
                        CreatedAt = DateTime.UtcNow
                };

                    // Add the new record to the database
                    _dbContext.MealPlans.Add(newMealPlan);
                }
                _dbContext.SaveChanges();

                _logger.LogInformation($"MealPlan and grocerylist generated for UserId: {userTaskDTO.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating MealPlan");
            }
        }
        async Task FillUserMealsData(aMeal meal)
        {
            meal.Recipe = await GetMealRecipe(meal);

            string systemPromptJsonMealDataGeneration = @"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.";

            string userPromptJsonMealDataGeneration = $@"
Create a concise JSON summary for this recipe:

{meal.Recipe}


This summary will provide a quick visual representation of key information as tags and assist in generating grocery lists. Include the following elements:

Meal Name: Specify the name of the dish.

Macronutrient Tags: List each macronutrient (proteins, carbohydrates, fats, and calories) with their respective quantities in grams (g) or calories (kcal).

Micronutrient Tags: Provide a simple list of key micronutrients present in the meal, formatted as tags. Include only the names of these nutrients.

Serving Size Tag: Add a tag for the serving size, indicating the quantity in grams of the final prepared dish.

Ingredient List for Groceries: List all the ingredients used in the recipe, without specifying quantities. This list will be used for generating grocery lists. add items even if they are optional.


This structure of the json should be used consistently for different theUserMealsRoot, with only the data values changing:

{{
  ""MealName"": ""Classic Spaghetti Carbonara"",
  ""Macronutrients"": {{
    ""Proteins"": ""40g"",
    ""Carbohydrates"": ""75g"",
    ""Fats"": ""35g"",
    ""Calories"": ""800 kcal""
  }},
  ""Micronutrients"": [""Calcium"", ""Vitamin B12"", ""Zinc""],
  ""ServingSize"": ""500g"",
  ""Ingredients"": [""Spaghetti"", ""Egg"", ""Pancetta"", ""Pecorino Cheese"", ""Parmesan Cheese"", ""Black Pepper"", ""Salt"", ""Garlic Powder""]
}}

";

            var userMealDataJson = GenerateJsonBasedOnPromptResponse(_openAiApi, systemPromptJsonMealDataGeneration, userPromptJsonMealDataGeneration);

            var MealData = JsonConvert.DeserializeObject<MealData>(userMealDataJson);

            meal.Macronutrients = MealData.Macronutrients;
            meal.Micronutrients = MealData.Micronutrients;
            meal.ServingSize = MealData.ServingSize;
            meal.Ingredients = MealData.Ingredients;
        }

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
                    await File.WriteAllBytesAsync(localFilePath, imageBytes);
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
        private async Task<string> GetMealRecipe(aMeal meal)
        {

            var chatRecipeGeneration = CreateConversationGPT4(_openAiApi);
            string systemPrompt = @"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                User information relevant informations is provided in the following JSON format.
                                {
                                    ""userDetails"": {
                                    ""name"": ""Ayoub"",
                                    ""age"": 25,
                                    ""gender"": ""Male"",
                                    ""weight"": ""68 kg"",
                                    ""height"": ""192 cm"",
                                    ""activityLevel"": ""Regularly Active"",
                                    ""foodAllergies"": [],
                                    ""additionalAllergyNotes"": """",
                                    ""healthConditions"": [""None""],
                                    ""additionalHealthNotes"": """"
                                    },
                                    ""nutritionalGoals"": {
                                    ""primaryGoal"": ""Weight Gain and build strong BIG muscles"",
                                    ""secondaryGoals"": [""Build BIG strong muscles"",""Energy and Stamina Enhancement"", ""Digestive Health"", ""Mental Wellness and Focus""],
                                    ""nutritionKnowledgeLevel"": ""Beginner"",
                                    ""weightManagementSpecifics"": ""Fast Weight Gain"",
                                    ""goalWeight"": ""75 kg"",
                                    ""dietaryPreferences"": {
                                        ""foodSource"": [""None Specified""],
                                        ""macronutrientFocus"": [""Let our AI choose based on your nutritional goals (Recommended)""]
                                    },
                                    ""managingHealthConditions"": [""None""],
                                    ""managingMentalHealthConditions"": [""None""],
                                    ""additionalNutritionalInformation"": """"
                                    },
                                    ""mealPlanPreferences"": {
                                    ""cookingSkillLevel"": ""Some Experience"",
                                    ""mealSizePreference"": ""Larger Main Meals"",
                                    ""favoriteCuisines"": [""Not Specified""],
                                    ""favoriteDishCategories"": [""Pasta Dishes"", ""Grilled Foods"", ""Stir-Fries""],
                                    ""groceryListPreferences"": {
                                        ""shoppingStyle"": ""Balanced"",
                                        ""stapleItems"": [""Not Specified""],
                                        ""dislikedIngredients"": [],
                                        ""idealCookingTime"": ""Moderately Involved"",
                                        ""kitchenAppliances"": [""Stove/Oven"", ""Blender""]
                                    },
                                    ""snackingHabits"": ""Occasionally"",
                                    ""supplementUse"": [""Protein Powders""],
                                    ""budgetConstraints"": ""Moderate Budget""
                                    }
                                }";

            string userRecipeGenerationprompt = $@"Create a personalized and engaging recipe for this meal 
{meal.ToString()}
Ensure the recipe is tailored to the user, with a friendly, chef-like tone, incorporating these specific elements:

Personalized Introduction: Begin with a warm, personalized greeting, mentioning the user's name and introducing the recipe.

Recipe Name and Description: Clearly state the recipe name and provide an engaging description, highlighting why this meal is beneficial for the user.

Serving Size Clarification: Specify the serving size in grams for the final prepared dish, making it clear that this refers to the ready-to-serve meal.

ingredients List: Detail the ingredients for one serving (specify it explicitly), with easy-to-follow quantities.

Appliances/Tools Needed: List the kitchen appliances and tools required to prepare the meal, ensuring users are well-prepared before starting.

Detailed Cooking Instructions: Format each step in bold, followed by a detailed, easy-to-follow explanation. Make the instructions engaging and motivating, encouraging users to enjoy the cooking process. the more

Meal Timing Recommendations: Include advice on the best time of day to enjoy this meal, and when it might be less ideal, based on its nutritional content.

Markdown Format: Use Markdown for clear formatting, emphasizing important sections in bold.

Personal Touch: Conclude with a message that reinforces how this meal contributes to the user's health and enjoyment.

Macronutrient and Micronutrient Breakdown: At the end of the recipe, provide a detailed breakdown of macronutrients (calories, carbohydrates, proteins, fats) and key micronutrients emphasizing their health benefits and relevance to the user. This is placed last to ensure the language model has full context of the ingredients, quantities, and serving size for more accurate calculations.

Conclude with a summary

Note for GPT : Important data and conclusions should be bolded for emphasis

Your goal is to create a recipe that is nutritionally informative, fun and easy to follow, and resonates personally with the user, inspiring them to confidently prepare and enjoy the meal.

Food is not just to nourish the body

Food nourishes the soul 

You have to romance people

It has to be something that makes people go, omg, I can't wait to get a fork and dig into that

Use a conversational tone, as a chef is speaking directly to them, starting by greeting the user by their name.

Important Note for GPT : PLZ DIRECTLY START BY GENERATING THE MARKDOWN, !!!DO NOT write something before like ""---"" or ""```markdown""

Start directly by the markdown header  : # The Title of the Recipe
";
            chatRecipeGeneration.AppendSystemMessage(systemPrompt);
            chatRecipeGeneration.AppendUserInput(userRecipeGenerationprompt);

            var chatRecipePromptResponse = await chatRecipeGeneration.GetResponseFromChatbotAsync();


            return chatRecipePromptResponse;
        }

        private Conversation CreateConversationGPT4(OpenAIAPI iOpenAIAPI)
        {
            _logger.LogInformation("CreateConversation: Starting creation of new conversation");

            try
            {
                var chat = iOpenAIAPI.Chat.CreateConversation();

                chat.RequestParameters.Temperature = 0.2;
                chat.RequestParameters.MaxTokens = 5000;
                chat.Model = OpenAI_API.Models.Model.GPT4;


                _logger.LogInformation("CreateConversation: Successfully created a new conversation");

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError("CreateConversation: An error occurred while creating conversation: {Message}", ex.Message);
                throw;
            }
        }

        private Conversation CreateConversationGPT4_6000token(OpenAIAPI iOpenAIAPI)
        {
            _logger.LogInformation("CreateConversation: Starting creation of new conversation");

            try
            {
                var chat = iOpenAIAPI.Chat.CreateConversation();

                chat.RequestParameters.Temperature = 0.2;
                chat.RequestParameters.MaxTokens = 6000;
                chat.Model = OpenAI_API.Models.Model.GPT4;


                _logger.LogInformation("CreateConversation: Successfully created a new conversation");

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError("CreateConversation: An error occurred while creating conversation: {Message}", ex.Message);
                throw;
            }
        }


        private string GenerateJsonBasedOnPromptResponse(OpenAIAPI iOpenAIAPI, string systemPromptJson, string userPromptJson)
        {
            _logger.LogInformation("CreateConversation: Starting creation of new conversation");

            try
            {
                ChatRequest chatRequest = new ChatRequest()
                {
                    Model = "gpt-3.5-turbo-1106",
                    Temperature = 0.0,
                    MaxTokens = 1000,
                    ResponseFormat = ChatRequest.ResponseFormats.JsonObject,
                    Messages = new ChatMessage[] {
                    new ChatMessage(ChatMessageRole.System, systemPromptJson),
                    new ChatMessage(ChatMessageRole.User, userPromptJson)
                }
                };
                var chat = iOpenAIAPI.Chat.CreateChatCompletionAsync(chatRequest).Result;


                _logger.LogInformation("CreateConversation: Successfully created a new conversation");

                return chat.Choices.FirstOrDefault().ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError("CreateConversation: An error occurred while creating conversation: {Message}", ex.Message);
                throw;
            }
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
        private string GetMealGroceryList(string recipe)
        {
            var match = Regex.Match(recipe, @"ingredients.*?:\n([\s\S]*?)\n\n🍳", RegexOptions.Multiline);

            if (match.Success)
            {
                string ingredientsSection = match.Groups[1].Value.Trim();
                return ingredientsSection;
            }
            else
            {
                return "ingredients not found, dude! 😬";
            }
        }


        private async Task<string> GetDailyGroceryList(string results)
        {

            var chat = CreateConversation(_openAiApi);


            string prompt = $@"{{Using the provided (results) which contains detailed grocery lists for theUserMealsRoot throughout the week, generate an aggregated and organized grocery list. The list should consolidate similar items, account for total quantities needed, and be presented in a fun, easy-to-read manner.  😎🤘```

                                    This prompt should be perfect to get that grocery list organized and rockin'!   🕺🚀🎉
                                    - 🧮 **IMPORTANT**: Add up those quantities. We don't want six separate lines for eggs, do we? 🥚🥚🥚🥚🥚🥚
                                    - And don't forget, let your creativity run wild based on the ingredients and the user's vibes.    
                                    Note: Please use emojis in the result for a more pleasant visual experience.Get creative with emojis to make that list pop! 🎉🔥 😊
                                    And please use Markdown format for the result. 📝
                                    {results}
                                }}";

            string exampleChatbotOutput = @"
                                        # 🛒 Ayoub's Ultimate Daily Grocery List🎉

                                        ### 🥩 **Meat Party:**

                                        ### 🌱 **Veggie Vibes:**
                                        ----------

                                        ### 🧀 **Dairy Delights:**
                                        --------
                                        ### 🥚 **Eggy Essentials:**
                                        -----

                                        ### 🌰 **Condiments & Sauces:**
                                        ------

                                        ### 🌿 **Herbs & Spices:**
                                        ------
                                        ";

            chat.AppendExampleChatbotOutput(exampleChatbotOutput);
            chat.AppendUserInput(prompt);

            var PromptResponse = await chat.GetResponseFromChatbotAsync();
            return PromptResponse;
        }

    }

    public class MealData
    {
        public string MealName { get; set; }
        public Macronutrients Macronutrients { get; set; }
        public List<string> Micronutrients { get; set; }
        public string ServingSize { get; set; }
        public List<string> Ingredients { get; set; }
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
    public class aMeal
    {
        public string MealName { get; set; }
        public List<string> MealType { get; set; }
        public List<string> PreparationType { get; set; }
        public List<string> MoodSuitability { get; set; }
        public Macronutrients Macronutrients { get; set; }
        public List<string> Micronutrients { get; set; }
        public string ServingSize { get; set; }
        public List<string> Ingredients { get; set; }


        public string Recipe { get; set; }  // <-- New property

        public string MealImage { get; set; }

        public override string ToString()
        {
            var stringBuilder = new System.Text.StringBuilder();

            stringBuilder.AppendLine($"Meal Name: {MealName}");

            if (MealType != null && MealType.Any())
                stringBuilder.AppendLine($"Meal Type: {string.Join(", ", MealType)}");

            if (PreparationType != null && PreparationType.Any())
                stringBuilder.AppendLine($"Preparation Type: {string.Join(", ", PreparationType)}");

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






}