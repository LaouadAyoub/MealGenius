using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System.Buffers.Text;
using System.ComponentModel.Design;
using System.Net.NetworkInformation;
using System.Runtime.Intrinsics.X86;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
// OpenAI_API.Models.Model.GPT4


namespace MealGeniusBackend.Services.Dashboard
{
    public interface IGroceryListService
    {
        Task GenerateGroceryList(UserTaskDTO userTaskDTO);
    }


    public class GroceryListService : IGroceryListService
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<MealPlanService> _logger;
        private readonly IOpenAIService _openAIService;
        private readonly IAzureBlobService _azureBlobService;
        private readonly ImageService _ImageService;
        public GroceryListService(UserDbContext userDbContext, ILogger<MealPlanService> logger, IOpenAIService openAIService, IAzureBlobService azureBlobService, ImageService imageService)
        {
            _dbContext = userDbContext;
            _logger = logger;
            _openAIService = openAIService;
            _azureBlobService = azureBlobService;
            _ImageService = imageService;
        }
        public async Task GenerateGroceryList(UserTaskDTO userTaskDTO)
        {
            var userTask = _dbContext.Tasks.Where(u => u.Id == userTaskDTO.Id).FirstOrDefault();

            try
            {
                userTask.GroceryListsGenerationExcecutedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
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

                if (existingMealPlan is null)
                {
                    throw new Exception("No meal plan found to generate the grocery list");
                }

                if (!existingMealPlan.GroceryListJson.IsNullOrEmpty())
                {
                    if (userTaskDTO.Status == UserTaskStatus.TobeRetried)
                    {
                        //_dbContext.UserDashboards.Remove(existingDashboard);
                        existingMealPlan.GroceryListJson = "";
                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogInformation("GroceryList already exists for this task");
                        return;
                    }
                }

                //Deserialize the meal plan json
                var timer = new ServiceTaskTimer("GroceryList", "The generation of : Generate a JSON file that contains a comprehensive grocery list for a user");
                timer.Start();
                var myMealPlan = JsonConvert.DeserializeObject<UserMealsRoot>(existingMealPlan.MealPlanJson);

                var groceryList_JsonExamplePath = "JsonFiles/Grocery_List_Example.json";
                string groceryList_JsonExample = File.ReadAllText(groceryList_JsonExamplePath);

                var UserInputsJson = userInput.UserData;
                //GroceryList Generation

                string systemPromptGroceryListGeneration = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.


                            It focuses on setting nutritional goals and providing tailored meal plans based on user data.
                            The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                            MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                            You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                            User information relevant informations is provided in the following JSON format.
                            {UserInputsJson}
            ";
                string userPromptGroceryListGeneration = $@"
Generate a JSON file that contains a comprehensive grocery list for a user,
based on the following list of ingredients required to prepare their meals.
[list of ingredients : this list contains the list of ingredients that the user needs to prepare all their meals :
{myMealPlan!.DisplayAllIngredients()} ]

The JSON should include an array of unique grocery items, ensuring that if an ingredient is mentioned more than once,
it is listed only once in the final JSON file.
Additionally, if the list includes prepared food items (e.g., ""Grilled Chicken Breast""),these should be converted into their raw, grocery-store equivalent (e.g., ""Chicken Breast"") to reflect items that are commonly found in grocery stores and supermarkets.
This approach ensures the grocery list is practical for shopping, focusing on the ingredients' most basic and purchasable forms.
The JSON structure should follow this Jsonformat:{groceryList_JsonExample}
";
                var GroceryList_Json = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptGroceryListGeneration, userPromptGroceryListGeneration, maxTokens: 4000, model: "gpt-4-1106-preview", temperature: 0);

                var groceryList = JsonConvert.DeserializeObject<GroceryList>(GroceryList_Json);

                timer.StopAndLog();


                // Loop over each category and convert the list of grocery items to a string
                var groceryCategoriesDetailed = new GroceryCategoriesDetailed();

                var tasks = new List<Task>();

                foreach (var category in groceryList.Categories)
                {
                    //string formattedGroceryItems = $"Category Name:{category.Key} \n Grocery Items: {string.Join(", ", category.Value)}";
                    GroceryCategory groceryCategory = new GroceryCategory
                    {
                        CategoryName = category.Key,
                        GroceryItems = new List<GroceryItem>()
                    };

                    // A HashSet to track unique grocery item names for this category
                    HashSet<string> uniqueItemNames = new HashSet<string>();

                    foreach (var itemName in category.Value)
                    {
                        // Check if the item name is already added
                        if (!uniqueItemNames.Contains(itemName))
                        {
                            GroceryItem groceryItem = new GroceryItem
                            {
                                GroceryItemName = itemName
                            };

                            // Add the unique grocery item to the category and the name to the tracking HashSet
                            groceryCategory.GroceryItems.Add(groceryItem);
                            uniqueItemNames.Add(itemName);
                        }
                    }

                    groceryCategoriesDetailed.GroceryCategories.Add(groceryCategory);

                }


                var timer1 = new ServiceTaskTimer("GroceryListService", " GenerateGroceryInfos");
                timer1.Start();
                foreach (var category in groceryCategoriesDetailed.GroceryCategories)
                {
                    tasks.Add(GenerateGroceryInfos(category, UserInputsJson));
                }

                await Task.WhenAll(tasks);

                timer1.StopAndLog();


                existingMealPlan.GroceryListJson = JsonConvert.SerializeObject(groceryCategoriesDetailed);

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Grocerylist generated for UserId: {userTaskDTO.UserId}");

                // Generate images for grocery items
                var existing_groceryList = JsonConvert.DeserializeObject<GroceryCategoriesDetailed>(existingMealPlan.GroceryListJson);

                foreach (var groceryCategory in existing_groceryList.GroceryCategories)
                {
                    foreach (var groceryItem in groceryCategory.GroceryItems)
                    {
                        if (!groceryItem.GroceryItem_ImageUrl.IsNullOrEmpty())
                            continue;

                        // verifier si l'image du groceryItem existe dans la table GroceryItemImages
                        //public DbSet<GroceryItem> GroceryItems { get; set; }  // Ajout du nouveau DbSet

                        var existingGroceryItem = _dbContext.GroceryItems.FirstOrDefault(g =>
                            g.Name.ToLower() == groceryItem.GroceryItemName.ToLower() &&
                            !string.IsNullOrEmpty(g.ImageUrl)); var groceryItemSimilarNamesToLower = groceryItem.SimilarNames.Select(name => name.ToLower()).ToList();
                        bool imageFound = false;

                        //verifier si il existe dans groceryItem.SimilarNames
                        if (existingGroceryItem != null && !existingGroceryItem.ImageUrl.IsNullOrEmpty())
                        {
                            groceryItem.GroceryItem_ImageUrl = existingGroceryItem.CompressedImageUrl;
                            imageFound = true;
                            existingGroceryItem.SimilarNames = groceryItemSimilarNamesToLower;
                            // Ensure existingSimilarNames is not null and is a List<string>
                            var existingSimilarNames = existingGroceryItem.SimilarNames ?? new List<string>();

                            // Add new similar names if they don't already exist in the existingSimilarNames list
                            foreach (var name in groceryItemSimilarNamesToLower)
                            {
                                if (!existingSimilarNames.Contains(name))
                                {
                                    existingSimilarNames.Add(name);
                                }
                            }
                            // Update the existingGroceryItem's SimilarNames with the updated list
                            existingGroceryItem.SimilarNames = existingSimilarNames;

                            continue;
                        }
                        string itemNameToLower = groceryItem.GroceryItemName.ToLower();
                        // Vérifier si l'un des noms similaires correspond exactement à item.Name (en tenant compte de la casse)

                        foreach (var groceryItemDb in _dbContext.GroceryItems)
                        {
                            if (!groceryItemDb.SimilarNames.IsNullOrEmpty())
                            {
                                var matchFoundInSimilarNames = groceryItemDb.SimilarNames.Any(similarName => similarName.ToLower() == itemNameToLower);
                                if (matchFoundInSimilarNames)
                                {

                                    groceryItem.GroceryItem_ImageUrl = groceryItemDb.CompressedImageUrl;
                                    imageFound = true;
                                    var existingSimilarNames = groceryItemDb.SimilarNames ?? new List<string>();
                                    //var matchFoundInExistingSimilarNames = groceryItem.SimilarNames.Any(similarName => similarName.ToLower() == name.ToLower());

                                    foreach (var name in groceryItem.SimilarNames)
                                    {
                                        var matchFoundInExistingSimilarNames = existingSimilarNames.Any(similarName => similarName.ToLower() == name.ToLower());
                                        if (matchFoundInExistingSimilarNames)
                                        {
                                            existingSimilarNames.Add(name);
                                        }
                                    }

                                    // Update the existingGroceryItem's SimilarNames with the updated list
                                    groceryItemDb.SimilarNames = existingSimilarNames;

                                    break;
                                }
                            }
                        }
                        if (imageFound)
                        {
                            existingMealPlan.GroceryListJson = JsonConvert.SerializeObject(existing_groceryList);

                            await _dbContext.SaveChangesAsync();
                            continue;
                        }

                        var existingSimilarGroceryItem = _dbContext.GroceryItems.FirstOrDefault(g => g.SimilarNames.Contains(groceryItem.GroceryItemName));

                        if (existingSimilarGroceryItem != null)
                        {
                            groceryItem.GroceryItem_ImageUrl = existingSimilarGroceryItem.CompressedImageUrl;
                            imageFound = true;

                            existingSimilarGroceryItem.SimilarNames = groceryItemSimilarNamesToLower;
                            // Ensure existingSimilarNames is not null and is a List<string>
                            var existingSimilarNames = existingSimilarGroceryItem.SimilarNames ?? new List<string>();
                            foreach (var name in groceryItemSimilarNamesToLower)
                            {
                                if (!existingSimilarNames.Contains(name))
                                {
                                    existingSimilarNames.Add(name);
                                }
                            }

                            // Update the existingGroceryItem's SimilarNames with the updated list
                            existingGroceryItem.SimilarNames = existingSimilarNames;
                            continue;
                        }
                        foreach (var similarName in groceryItem.SimilarNames)
                        {
                            var existingSimilarItem = _dbContext.GroceryItems.FirstOrDefault(g => g.Name.ToLower() == similarName.ToLower());

                            if (existingSimilarItem != null && !existingSimilarItem.ImageUrl.IsNullOrEmpty())
                            {
                                groceryItem.GroceryItem_ImageUrl = existingSimilarItem.CompressedImageUrl;
                                imageFound = true;
                                break;
                            }
                            var existingSimilarGroceryItemInSimilarNames = _dbContext.GroceryItems.FirstOrDefault(g => g.SimilarNames.Contains(similarName));
                            if (existingSimilarGroceryItemInSimilarNames != null)
                            {
                                if (existingSimilarItem != null && !existingSimilarItem.ImageUrl.IsNullOrEmpty())
                                {
                                    groceryItem.GroceryItem_ImageUrl = existingSimilarItem.CompressedImageUrl;
                                    imageFound = true;
                                    break;
                                }

                            }
                        }
                        if (imageFound)
                        {
                            existingMealPlan.GroceryListJson = JsonConvert.SerializeObject(existing_groceryList);

                            await _dbContext.SaveChangesAsync();
                            continue;
                        }
                        //Make a list of strings to one string string1, string2,string3

                        var existingNotFoundGroceryItem = _dbContext.NotFoundGroceryItems.FirstOrDefault(g => g.Name.ToLower() == groceryItem.GroceryItemName.ToLower());
                        if (existingNotFoundGroceryItem is null)
                        {
                            groceryItemSimilarNamesToLower = groceryItem.SimilarNames.Select(name => name.ToLower()).ToList();


                            await _dbContext.NotFoundGroceryItems.AddAsync(new NotFoundGroceryItems { GroceryItemId = new Guid(), Name = groceryItem.GroceryItemName, Category = groceryCategory.CategoryName, SimilarGroceryItemFound = "", SimilarNames = groceryItemSimilarNamesToLower });
                        }



                    }
                }

                existingMealPlan.GroceryListJson = JsonConvert.SerializeObject(existing_groceryList);

                userTask.UserOutputStatus = UserOutputStatus.GroceryListCompleted;

                existingMealPlan.GroceryListVersion++;

                await _dbContext.SaveChangesAsync();

                return;


            }
            catch (Exception ex)
            {
                userTask.Status = UserTaskStatus.Failed;
                await _dbContext.SaveChangesAsync();
                _logger.LogError(ex, "Error while generating grocery list");
                throw;
            }
        }


        private async Task GenerateGroceryInfos(GroceryCategory groceryCategory, string UserInputsJson)
        {
            // Define the maximum number of items per segment
            const int MaxItemsPerSegment = 12;


            // Create a list to hold all the tasks
            List<Task<GroceryCategory>> tasks = new List<Task<GroceryCategory>>();

            // Determine the number of segments needed
            int segmentCount = (int)Math.Ceiling((double)groceryCategory.GroceryItems.Count / MaxItemsPerSegment);

            for (int i = 0; i < segmentCount; i++)
            {
                // Creating segments of GroceryItems
                var segment = groceryCategory.GroceryItems.Skip(i * MaxItemsPerSegment).Take(MaxItemsPerSegment).ToList();
                // Launch a task for each segment
                tasks.Add(ProcessSegmentAsync(groceryCategory.CategoryName, segment, UserInputsJson));
            }
            // Wait for all tasks to complete
            var results = await Task.WhenAll(tasks);

            // Combine all processed items from each segment
            List<GroceryItem> allProcessedItems = results.SelectMany(category => category.GroceryItems).ToList();

            foreach (var item in allProcessedItems)
            {
                if(item.EssentialNutrients == null)
                {
                   
                }
                if (item.Benefits == null)
                {

                }
            }

            groceryCategory.GroceryItems = allProcessedItems;

        }

        private async Task<GroceryCategory> ProcessSegmentAsync(string categoryName, List<GroceryItem> segment, string userInputsJson)
        {
            // Process each segment sequentially
            string formattedSegmentGroceryItems = $"Category Name:{categoryName} \n Grocery Items: {string.Join(", ", segment.Select(groceryItem => groceryItem.GroceryItemName))}";

            //string formattedGroceryItems = $"Category Name:{groceryCategory.CategoryName} \n Grocery Items: {string.Join(", ", groceryCategory.GroceryItems.Select(groceryItem => groceryItem.GroceryItemName))}";

            var groceryList_JsonExamplePath = "JsonFiles/Grocery_List_Detailed_Example.json";

            string groceryListDEtailed_JsonExample = File.ReadAllText(groceryList_JsonExamplePath);

            string systemPromptDetailedGroceryListGeneration = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.


                                It focuses on setting nutritional goals and providing tailored meal plans based on user data.
                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                User information relevant informations is provided in the following JSON format.
                                {userInputsJson}
            ";

            var userPromptDetailedGroceryListGeneration = $@"
                Given a category name and a list of grocery items, 
                generate a detailed grocery information JSON that adheres to the provided format.
                Each entry should include the name of the grocery item, its benefits, and a list of essential nutrients.
                The descriptions should be both informative and entertaining, in line with the MealGenius theme of making nutrition education enjoyable.
                The JSON structure should follow this format:
                {groceryListDEtailed_JsonExample}
        
                Because for each Grocery item, we have to generate the related image of the grocery item, to minimize the number of the image generated, you have to choose the grocery Item names to be the most pupular one 
                in the grocery store, and how the item is named and purchased in the grocery store, for example : instead of choosing Red Apple, you have to choose Apple as the grocery item name. 
                In similar names, you have to list also different names or nominations of the grocery item, for example, for Apple, we can find an image for Apples for example, the goal is to find an image for the grocery item that can be used for all the similar names of the grocery item, and to minimize the number of the image generated.
                GroceryItemName: Name of the grocery item
                SimilarNames : [List of similar names of the grocery item, Apples, Fresh apple]
                Essential_Nutrients: List the essentiel nutrients presented this this GroceryItemn
                Health benefits: Health benefits of this GroceryItem for the user explained simply and clearly.

                The list of grocery items is as follows:
                {formattedSegmentGroceryItems}
        

                Instructions for GPT:

                Make It Informative: Provide actual benefits and essential nutrients of each grocery item, showcasing its health benefits and nutritional content.

                Keep It Entertaining: Use playful language and creative descriptions to engage the reader, making the learning process about nutrition fun and memorable.

                Align with MealGenius Theme: Ensure each description supports the MealGenius mission of combining education with enjoyment, helping users to discover the joy in healthy eating.
                ";

            var GroceryListDetailed_Json = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptDetailedGroceryListGeneration, userPromptDetailedGroceryListGeneration, maxTokens: 4000, model: "gpt-4-1106-preview", temperature: 0.5);


            var groceryListDetailed = JsonConvert.DeserializeObject<GroceryCategory>(GroceryListDetailed_Json);

            return groceryListDetailed;
        }

    }


    public class GroceryList
    {
        [JsonProperty("GroceryList")]
        public Dictionary<string, List<string>> Categories { get; set; }
    }


    public class GroceryCategoriesDetailed
    {
        public List<GroceryCategory> GroceryCategories { get; set; } = new List<GroceryCategory>();
    }


    public class GroceryCategory
    {
        [JsonProperty("CategoryName")]
        public string CategoryName { get; set; }

        [JsonProperty("GroceryItems")]
        public List<GroceryItem> GroceryItems { get; set; } = new List<GroceryItem>();
    }

    // GroceryItem remains the same
    public class GroceryItem
    {
        [JsonProperty("GroceryItemName")]
        public string GroceryItemName { get; set; }

        [JsonProperty("SimilarNames")]
        public List<string> SimilarNames { get; set; }

        [JsonProperty("HealthBenefits")]
        public string Benefits { get; set; }

        [JsonProperty("Essential_Nutrients")]
        public List<string> EssentialNutrients { get; set; }
        public string GroceryItem_ImageUrl { get; set; }
    }

}