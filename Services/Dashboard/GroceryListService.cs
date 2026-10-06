using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Helpers;
using MealGeniusBackend.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Services.Dashboard
{
    public interface IGroceryListService { Task GenerateGroceryList(UserTaskDTO task); }
    public class GroceryListService(UserDbContext _dbContext, IOpenAIService _openAIService,
        ILogger<GroceryListService> _logger) : IGroceryListService
    {
        public async Task GenerateGroceryList(UserTaskDTO userTaskDTO)
        {
            var userTask = await _dbContext.Tasks.SingleAsync(t => t.Id == userTaskDTO.Id);
            var userInput = await _dbContext.UserInputs.SingleAsync(i => i.TaskId == userTaskDTO.Id);
            var existingMealPlan = await _dbContext.MealPlans.SingleAsync(p => p.TaskId == userTaskDTO.Id);
            if (existingMealPlan.GroceryListVersion > 0) return;
            userTask.GroceryListsGenerationExcecutedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            if (string.IsNullOrWhiteSpace(existingMealPlan.GroceryListJson))
            {
                var timer = new ServiceTaskTimer("GroceryList", "The generation of : Generate a JSON file that contains a comprehensive grocery list for a user");
                timer.Start();
                var myMealPlan = JsonConvert.DeserializeObject<UserMealsRoot>(existingMealPlan.MealPlanJson);

                var groceryList_JsonExamplePath = "JsonFiles/Grocery_List_Example.json";
                string groceryList_JsonExample = File.ReadAllText(groceryList_JsonExamplePath);

                var UserInputsJson = PromptPrivacy.RemoveEmail(userInput.UserData);
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
                var GroceryList_Json = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptGroceryListGeneration, userPromptGroceryListGeneration, maxTokens: 4000, temperature: 0);

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
            }
            // Nonempty JSON is a checkpoint, not proof that enrichment finished.
            var groceries = JsonConvert.DeserializeObject<GroceryCategoriesDetailed>(existingMealPlan.GroceryListJson)
                ?? throw new InvalidDataException("Missing grocery categories.");
            if (groceries.GroceryCategories.Count == 0) throw new InvalidDataException("Empty grocery categories.");
            var catalogue = await _dbContext.GroceryItems.ToListAsync();
            var missingNames = new HashSet<string>(await _dbContext.NotFoundGroceryItems.Select(i => i.Name).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);
            foreach (var category in groceries.GroceryCategories)
            foreach (var item in category.GroceryItems)
            {
                if (string.IsNullOrWhiteSpace(item.GroceryItemName)) throw new InvalidDataException("Unnamed grocery item.");
                var match = GroceryImageMatcher.Find(catalogue, item.GroceryItemName, item.SimilarNames);
                if (match is not null)
                {
                    item.GroceryItem_ImageUrl = string.IsNullOrWhiteSpace(match.CompressedImageUrl) ? match.ImageUrl : match.CompressedImageUrl;
                    match.SimilarNames = GroceryImageMatcher.MergeAliases(match.SimilarNames, item.SimilarNames);
                }
                else if (missingNames.Add(item.GroceryItemName))
                    _dbContext.NotFoundGroceryItems.Add(new NotFoundGroceryItems
                    {
                        GroceryItemId = Guid.NewGuid(), Name = item.GroceryItemName, Category = category.CategoryName,
                        SimilarNames = item.SimilarNames ?? [], SimilarGroceryItemFound = ""
                    });
            }
            existingMealPlan.GroceryListJson = JsonConvert.SerializeObject(groceries);
            existingMealPlan.GroceryListVersion = 1;
            userTask.UserOutputStatus = UserOutputStatus.GroceryListCompleted;
            await _dbContext.SaveChangesAsync();
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

            var GroceryListDetailed_Json = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptDetailedGroceryListGeneration, userPromptDetailedGroceryListGeneration, maxTokens: 4000, temperature: 0.5);


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