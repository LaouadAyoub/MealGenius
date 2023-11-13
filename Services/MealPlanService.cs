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

                string filePath = @"C:\persoProjects\MealPlanner\MealgeniusFull\MealGeniusBackend\SimplifiedWeeklyMealPlanJsonFile.json"; // Replace with the actual path
                var firstPromptResponseJson = File.ReadAllText(filePath);
                WeekPlan myWeekPlan = System.Text.Json.JsonSerializer.Deserialize<WeekPlan>(firstPromptResponseJson);
                var tasks = new List<Task>();

                foreach (var dayMealPlan in myWeekPlan.DayMealPlans)
                {
                    foreach (var meal in dayMealPlan.Meals)
                    {
                        tasks.Add(FillRecipe(meal));
                    }
                }
                await Task.WhenAll(tasks);


                //TODO : should i really serialise/deserialise ?
                UserInputDataModel userDataModel = JsonConvert.DeserializeObject<UserInputDataModel>(userInput.UserData);

                var settings = new JsonSerializerSettings
                {
                    StringEscapeHandling = StringEscapeHandling.Default
                };
                string mealPlan = Newtonsoft.Json.JsonConvert.SerializeObject(myWeekPlan, Formatting.Indented, settings);

                // some linq to get a random meal
                var fourthMeal = myWeekPlan.DayMealPlans.FirstOrDefault().Meals.FirstOrDefault();

                #region generate grocery list
                //generate weeklyGroceryList
                //List<StringBuilder> fullGroceryListForWeek = new List<StringBuilder>();

                //// Loop through the weekly plan
                //foreach (var dayMealPlan in myWeekPlan.DayMealPlans)
                //{
                //    StringBuilder dailyGroceryList = new StringBuilder();

                //    foreach (var meal in dayMealPlan.Meals)
                //    {
                //        // Get the grocery list for the current meal
                //        var grocerylist = GetMealGroceryList(meal.Recipe);

                //        // Append meal type and the grocery list to the daily list
                //        dailyGroceryList.AppendLine($"{meal.MealType}");
                //        dailyGroceryList.AppendLine("Ingredients:");
                //        dailyGroceryList.AppendLine(grocerylist);
                //        dailyGroceryList.AppendLine(); // Add an empty line for spacing
                //    }

                //    // Append daily grocery list to the weekly list
                //    fullGroceryListForWeek.Add(dailyGroceryList);
                //}

                //// Convert the StringBuilder to a string
                //List<Task<string>> GroceryTasks = new List<Task<string>>();
                //// Loop through each StringBuilder in fullGroceryListForWeek
                //foreach (var dailyList in fullGroceryListForWeek)
                //{
                //    // Start a new task for each dailyList
                //    Task<string> task = GetDailyGroceryList(dailyList.ToString());

                //    // Add the task to the list
                //    GroceryTasks.Add(task);
                //}
                //// Wait for all tasks to complete and retrieve the results
                //string[] allResults = await Task.WhenAll(GroceryTasks);
                //string finalList = string.Join(Environment.NewLine + Environment.NewLine, allResults);

                //var weeklyGRoceryList = await GetDailyGroceryList(finalList);
                #endregion

                var groceryList = @"# 🛒 Ayoub's Ultimate Weekly Grocery List 🎉

### 🥩 **Meat Party:**
- Ground Beef: 1.2kg 🥩
- Pork Belly: 400g 🐖
- Chicken Breast: 2kg 🍗
- Chicken Drumsticks: 1.5 kg 🍗
- Chicken Thighs: 1 kg 🍗
- Pork Ribs: 2 kg 🍖
- Bacon: 60g 🥓
- Fresh Salmon: 150g 🐟
- Assorted Sushi Fish: 200g 🐟
- Shrimp: 200g 🍤

### 🌱 **Veggie Vibes:**
- Lettuce: 230g 🥬
- Tomatoes: 850g 🍅
- Onion: 1.6kg 🧅
- Garlic: 46 Cloves 🧄
- Jalapeno Peppers: 50g 🌶️
- Bell Peppers: 800g 🌶️
- Ramen Noodles: 200g 🍜
- Bok Choy: 100g 🥬
- Bean Sprouts: 50g 🌱
- Green Onions: 300g 🧅
- Cabbage: 400g 🥬
- Carrots: 800g 🥕
- Zucchini: 700g 🥒
- Avocado: 350g 🥑
- Cucumber: 150g 🥒

### 🌾 **Grains & Pulses:**
- All-Purpose Flour: 1.2kg 🌾
- Tortillas: 500g 🌮
- Ramen Noodles: 200g 🍜
- Whole Black Lentils (Sabut Urad Dal): 250g 🌑
- Red Kidney Beans (Rajma): 50g 🍒
- Pizza Dough: 1250g 🍞
- Breadcrumbs: 200g 🍞
- Rice: 650g 🍚
- Sushi Rice: 500g 🍚
- Spaghetti: 300g 🍝
- Bread: 8 slices 🍞

### 🧀 **Dairy Delights:**
- Cheddar Cheese: 430g 🧀
- Parmesan Cheese: 100g 🧀
- Mozzarella Cheese: 600g 🧀
- Cream Cheese: 50g 🧀
- Paneer (Indian cottage cheese): 500g 🧀
- Yogurt: 100g 🥛
- Greek Yogurt: 80g 🥛
- Egg: 14 🥚
- Buttermilk: 250g 🥛
- Heavy Cream: 1 cup 🥛
- Butter: 200g 🧈
- Ghee (Clarified Butter): 2 tbsp 🧈

### 🍓 **Fruit Fun:**
- Jam: 2 tbsp 🍓
- Lemon Juice: 3 tbsp 🍋
- Lime Juice: 2 tbsp 🍋
- Pickled Ginger: 30g 🍣

### 🧂 **Condiments & Sauces:**
- Soy Sauce: 3/4 cup 🥣
- Olive Oil: 12 tbsp 🫒
- Sesame Oil: 1 tbsp 🌿
- Vegetable Oil: 6 tbsp 🌿
- Dijon Mustard: 30g 🌶️
- Yellow Mustard: 50g 🌭
- Mayonnaise: 250g 🥪
- Ketchup: 30g 🍅
- Salsa: 1 cup 🌶️
- Hoisin Sauce: 4 tbsp 🍯
- Tomato Sauce: 400g 🍅
- Mirin: 1 tbsp 🍶
- Coconut Milk: 1 cup 🥥

### 🌿 **Herbs & Spices:**
- Taco Seasoning: 2 tbsp 🌮
- Ginger: 2 tsp 🌟
- Salt: 30g 🧂
- Black Pepper: 10g 🌶️
- Chili Garlic Sauce: 1 tsp 🌶️
- Brown Sugar: 1 tsp 🍚
- Turmeric Powder: 3 tsp 🌟
- Ground Coriander: 2 tsp 🌿
- Ground Cumin: 5 tsp 🌿
- Paprika: 2 tsp 🌶️
- Curry Powder: 2 tbsp 🌶️
- Dried Oregano: 1 tsp 🌿
- Nutmeg: 1 tsp 🌰
- Ground Ginger: 1 tsp 🌿
- Szechuan Peppercorns: 1 tsp 🌶️
- Baking Powder: 1 tsp 🌟
- Cumin Seeds: 1 tsp 🌿
- Red Chili Powder: 1 tsp 🌶️
- Garam Masala: 1 tsp 🌿
- BBQ Spice Rub: 2 tbsp 💥
- Fresh Parsley: optional 🌿
- Fresh Cilantro: optional 🌿
- Fresh Mint Leaves: optional 🌿
- Dill Pickles: 50g 🥒
- Nori Sheets: optional 🌿

### 🍮 **Miscellaneous:**
- Sugar: 3 tsp 🍚
- Tandoori Masala: 1 tbsp 💥
- Fresh Basil Leaves: 22-24 🌿
- Wasabi Paste: 20g 🌶️
- Honey: 5 tbsp 🍯
- Vinegar: 30g 🍶
- Rice Vinegar: 4 tbsp 🍚
- Chickpea Flour (Besan): 2 tbsp 🌾
- Cornstarch: 110g 🌽
- Popblano Peppers: 8 🌶️
- Croissant: 1 🥐
- Biscuits: 4 🍪
- Naan Bread: 4 🍞
- Baguette or Italian Bread: 4 slices 🥖
- Tortilla Chips: 200g 🌽
- Ice Water: 1 cup ❄️

Now you're all set for the week! Start cooking and enjoy your meals! 🎉🔥 😊 🍽️ 🚀";
                // 2. Generate MealPlan info (Dummy data for now)
                MealPlan newMealPlan = new MealPlan
                {
                    Id = Guid.NewGuid(),
                    UserId = userTaskDTO.UserId,
                    TaskId = userTaskDTO.Id,
                    Title = "Sample Meal Plan",
                    MealPlanJson = mealPlan ,//myWeekPlan.DayMealPlans.Me,  // Contains the meals of the week : 28 Markdown in a json
                    GroceryListJson = groceryList // Contains the grocery list of the week
                };

                // 3. Store info in MealPlans table
                _dbContext.MealPlans.Add(newMealPlan);
                _dbContext.SaveChanges();

                _logger.LogInformation($"MealPlan and grocerylist generated for UserId: {userTaskDTO.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating MealPlan");
            }
        }
        async Task FillRecipe(Meal meal)
        {
            meal.Recipe = await GetMealPlan(meal);
        }
        private async Task<string> GetMealPlan(Meal meal)
        {

            var chat = CreateConversation(_openAiApi);


            string prompt = $@"{{Could you provide me with a {meal.MealName} recipe tailored for a 192cm man looking to gain weight? 🏋️‍♂️ Please include the following sections:

                                📝 Title of the Meal at the Top
                                📊 Nutritional Values Tailored for Weight Gain (Specify the total nutritional values in the meal for one person. Present this information in a table, and include the total calories and a percentage breakdown of proteins, fats, and carbohydrates. Also, specify the total weight of the meal.)
                                🍗🥕 Ingredients with Precise Weights in kg or grams
                                🍳 Preparation Instructions
                                💡 Why This Meal is Perfect for You! (Include sample data)
                                🌿🚫 Health Benefits and Cautions
                                Note: Please use emojis in the result for a more pleasant visual experience. 😊

                                And please use Markdown format for the result. 📝


                                }}";

            string exampleChatbotOutput = @"
**Moroccan Chicken Tajine for Weight Gain** 🍗🏋️‍♂️

⏲️ **Cooking Information**:
- 🕒 Prep Time: 30 minutes
- 🍳 Cook Time: 1 hour
- 🍽️ Serves: 4
- ⚖️ Total Weight of the Meal: 2 kg (approx.)
- 🍽️ Single Serving: 500g (approx.) with 300 kcal

📊 **Nutritional Values Tailored for Weight Gain**:

| Nutrient      | Amount    | Percentage of Total Calories | 
| ------------- | --------- | ---------------------------- | 
| Calories      | 1200 kcal | --                           | 
| Proteins      | 100g      | 33%                          | 
| Fats          | 56g       | 42%                          | 
| Carbohydrates | 90g       | 30%                          |
| Fiber         | 12g       | --                           |
| Vitamin A     | 5000 IU   | --                           |
| Vitamin C     | 60 mg     | --                           |

🍗 **Ingredients**:
- 1.5 kg Chicken Thighs, bone-in, skin-on 🍗
- 500g Butternut Squash, peeled and cubed 🎃
- 400g Carrots, peeled and sliced 🥕
- 300g Onion, chopped 🧅
- 4 Garlic Cloves, minced 🧄
- 2 tbsp Olive Oil 🫒
- 2 tbsp Moroccan Spice Blend 🌶️
- 1 tsp Turmeric Powder 🌟
- 1 tsp Ground Cumin 🌿
- 1 tsp Ground Ginger 🌿
- 1 tsp Ground Paprika 🌶️
- 1 tsp Salt 🧂
- 1/2 tsp Ground Black Pepper 🌶️
- 1 cup Chicken Broth 🥣
- Fresh Parsley, chopped (for garnish) 🌿

🍳 **Preparation Instructions**:
1. In a large bowl, mix together the Moroccan spice blend, turmeric powder, ground cumin, ground ginger, ground paprika, salt, and black pepper.
2. Coat the chicken thighs thoroughly with the spice mixture.
3. In a large tajine or deep skillet, heat the olive oil over medium heat.
4. Sauté the onion and garlic until they're golden brown and fragrant.
5. Add the seasoned chicken thighs and cook until browned on all sides.
6. Add the butternut squash, carrots, and chicken broth. Mix well.
7. Reduce heat to low, cover, and simmer for about 1 hour.
8. Garnish with fresh parsley before serving.

💡 **Why This Meal is Perfect for You**!  
This Moroccan Tajine is jam-packed with calories and essential nutrients, making it perfect for someone looking to gain weight. With 1200 kcal in total (300 kcal per serving) and a balance of protein, fats, and carbs, this meal will help you achieve your weight gain goals while keeping you satiated and energized.

🌿 **Health Benefits**  
This meal is rich in protein for muscle building, fiber for digestive health, and vitamins A and C for immune support.

🚫 **Cautions**  
Be cautious if you have any allergies to the listed ingredients. This is a high-calorie meal designed for weight gain, so adjust portions accordingly if you have different dietary needs.
There you have it! A Moroccan Tajine recipe loaded with emojis for maximum fun! 🎉🍲😄 Enjoy cooking, and bon appétit! 🍽️👌";

            chat.AppendExampleChatbotOutput(exampleChatbotOutput);
            chat.AppendUserInput(prompt);

            var PromptResponse = await chat.GetResponseFromChatbotAsync();
            string unescapedPromptResponse = Regex.Unescape(PromptResponse);

            return unescapedPromptResponse;
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
        private Conversation CreateConversationGPT4(OpenAIAPI iOpenAIAPI)
        {
            _logger.LogInformation("CreateConversation: Starting creation of new conversation");

            try
            {
                var chat = iOpenAIAPI.Chat.CreateConversation();

                chat.RequestParameters.Temperature = 0.2;
                chat.RequestParameters.MaxTokens = 4000;
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

        private string GetMealGroceryList(string recipe)
        {
            var match = Regex.Match(recipe, @"Ingredients.*?:\n([\s\S]*?)\n\n🍳", RegexOptions.Multiline);

            if (match.Success)
            {
                string ingredientsSection = match.Groups[1].Value.Trim();
                return ingredientsSection;
            }
            else
            {
                return "Ingredients not found, dude! 😬";
            }
        }

        private async Task<string> GetDailyGroceryList(string results)
        {

            var chat = CreateConversation(_openAiApi);


            string prompt = $@"{{Using the provided (results) which contains detailed grocery lists for meals throughout the week, generate an aggregated and organized grocery list. The list should consolidate similar items, account for total quantities needed, and be presented in a fun, easy-to-read manner.  😎🤘```

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






}