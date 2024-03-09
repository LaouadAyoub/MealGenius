using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using OpenAI_API;
using Stripe;
using System.Text;
// OpenAI_API.Models.Model.GPT4


namespace MealGeniusBackend.Services
{
    public interface IMealsImagesService
    {
        Task GenerateMealsImages(UserTaskDTO userTaskDTO);
    }



    public class MealsImagesService : IMealsImagesService
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<MealPlanService> _logger;
        private readonly IOpenAIService _openAIService;
        private readonly IAzureBlobService _azureBlobService;
        public MealsImagesService(UserDbContext userDbContext, ILogger<MealPlanService> logger, IOpenAIService openAIService, IAzureBlobService azureBlobService)
        {
            _dbContext = userDbContext;
            _logger = logger;
            _openAIService = openAIService;
            _azureBlobService = azureBlobService;
        }
        public async Task GenerateMealsImages(UserTaskDTO userTaskDTO)
        {
            var userTask = _dbContext.Tasks.Where(u => u.Id == userTaskDTO.Id).FirstOrDefault();
            _dbContext.SaveChanges();

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

                if (userTask.MealsImagesStatus is UserMealsImagesStatus.Completed)
                {
                    return;
                }
                if (existingMealPlan is null)
                {
                    _logger.LogError("MealPlan not found");
                    return;
                }


                // Generate First Json that contains the meals PreData
                var UserInputsJson = userInput.UserData;

                UserMealsRoot theUserMealsRoot = JsonConvert.DeserializeObject<UserMealsRoot>(existingMealPlan.MealPlanJson);


                // verifier si un et un seul meal contient un MealImage null of empty
                var mealWithNoImage = theUserMealsRoot.UserMeals.Where(m => string.IsNullOrEmpty(m.MealImage)).FirstOrDefault();

                if (mealWithNoImage is null)
                {
                    return;
                }
                userTask.MealsImagesStatus = UserMealsImagesStatus.Ongoing;
                _dbContext.SaveChanges();

                //Image Narrative Generation
                string systemPromptImageGeneration = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.

                                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                                User information relevant informations is provided in the following JSON format.
                                                {UserInputsJson}";

                string userImageNarrativeGeneration = $@"
                Based on the user infos and mood and from you creativity, Create a descriptive narrative for a set of meal images, defining a cohesive mood, style, and environment.
                This narrative will be used as the basis for generating images with DALL·E 3 . The images should visually represent a variety of Meals in a consistent and appealing manner. Consider these aspects in your description:

                Mood and Style: Describe the overall mood and artistic style of the images. Ensure that it's all about SIMPLICITY, and minimalistc style.

                Environment Setting: Define the setting or backdrop for the Meals. Is it an outdoor picnic, a cozy home kitchen, an upscale restaurant, or a casual café?

                Plate Presentation: Detail how the Meals should be presented on the plate. Should they be meticulously arranged, casually plated, or artistically styled?

                Color Palette: Suggest a color palette that should be consistent across all images. Consider colors that evoke the mood and complement the food.

                Additional Elements: Decide if there are any additional elements that should be included in every image, such as specific tableware, a particular type of garnish, or consistent lighting.

                This narrative will guide the creation of a series of meal images that are visually harmonious and aligned with the defined mood and setting. The goal is to ensure that each image, while unique in its meal presentation, shares a common aesthetic thread with the others.

                And remember : Food is not just to nourish the body

                Food nourishes the soul 

                You have to romance people

                It has to be something that makes people go, omg, I can't wait to get a fork and dig into that
                ";

                var chatImageNarrativeGenerationResponse = await _openAIService.GetResponseAsync(systemPromptImageGeneration, userImageNarrativeGeneration, OpenAI_API.Models.Model.ChatGPTTurbo, 4000);

                //Meal Recipe and PostData Generation
                int maxParallelTasks = 6; // Nombre maximal de tâches à exécuter en parallèle
                var userMeals = theUserMealsRoot.UserMeals;
                for (int i = 0; i < userMeals.Count; i += maxParallelTasks)
                {
                    var tasks = new List<Task>();

                    for (int j = i; j < i + maxParallelTasks && j < userMeals.Count; j++)
                    {
                        var meal = userMeals[j];
                        tasks.Add(GenerateMealsImages(meal, UserInputsJson, chatImageNarrativeGenerationResponse));
                    }

                    await Task.WhenAll(tasks);
                }
                //var tasks = new List<Task>();

                //foreach (var meal in theUserMealsRoot.UserMeals)
                //{
                //    if (tasks.Count >= maxParallelTasks)
                //    {
                //        // Attendre que toutes les tâches dans le lot actuel soient terminées
                //        await Task.WhenAll(tasks);
                //        tasks.Clear(); // Effacer la liste des tâches pour le prochain lot
                //    }

                //    // Ajouter la tâche de remplissage des données de repas à la liste
                //    tasks.Add(GenerateMealsImages(meal, UserInputsJson, chatImageNarrativeGenerationResponse));
                //}


                string mealPlanJson = Newtonsoft.Json.JsonConvert.SerializeObject(theUserMealsRoot);



                // If no record exists, create a new one
                existingMealPlan.MealPlanJson = mealPlanJson;

                var mealWithNoImageAfter = theUserMealsRoot.UserMeals.Where(m => string.IsNullOrEmpty(m.MealImage)).FirstOrDefault();

                if (mealWithNoImageAfter is null)
                {
                    userTask.MealsImagesStatus = UserMealsImagesStatus.Completed;
                    _logger.LogInformation($"All meal images were generated successfully");
                    _dbContext.SaveChanges();
                    return;
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating MealPlan");
                userTask.MealsImagesStatus = UserMealsImagesStatus.Failed;
                _dbContext.SaveChanges();
            }
        }

        async Task GenerateMealsImages(aMeal aMeal, string userData, string userImageNarrative)
        {
            try

            {

                // Image Generation
                if (!aMeal.MealImage.IsNullOrEmpty())
                {
                    return;
                }

                var mealRecipeBuilder = string.Join("\n", aMeal.mealRecipeBreakdown.DetailedCookingInstructions);
                string systemPrompt = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.

                                            It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                            The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                            MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                            You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                            User information relevant informations is provided in the following JSON format.
                                            {userData}";

                string userPromptImageGeneration = $@"
            Generate a detailed prompt for DALL·E 3 to create an image of a meal. This image should be based on the provided descriptive narrative, ensuring consistency in mood, style, and environment. 
            Additionally, incorporate specific details from the given recipe. 
            The prompt should blend these elements to guide the creation of an image that represents the meal accurately while adhering to the overall aesthetic theme.

            Please find the narrative below:
            {userImageNarrative}

            Here is the name of the meal : {aMeal.mealRecipeBreakdown.NameOfTheMeal}
            Please find the ingredients of the meal below:
            {aMeal.mealRecipeBreakdown.Ingredients}

            Please find the serving size of the meal below:
            {aMeal.mealRecipeBreakdown.ServingText}
            the serving size will allow you to estimate the size of the plate and the quantity of the meal.
            Please find Find the recipe of the meal below:
            {mealRecipeBuilder}


            Please acknowledge the whole recipe and the ingredients in the prompt so you can generate the image accordingly of how the Final meal should look like.
            Ensure the prompt doesnt exceed 3500 caracters.

            And remember : Food is not just to nourish the body

            Food nourishes the soul 

            You have to romance people

            It has to be something that makes people go, omg, I can't wait to get a fork and dig into that,

            Ensure that the image should be simple and clear, with a focus on the meal itself, so that it can be easily understood by users. and not be distracting or confusing.
            ";
                // TODO: maybe GPT-3 turbo is enoguh for this task
                var chatImagePromptResponse = await _openAIService.GetResponseAsync(systemPrompt, userPromptImageGeneration, OpenAI_API.Models.Model.GPT4_Turbo, 4000);


                var imageUrl = await _openAIService.GenerateImageForMealAsync(chatImagePromptResponse);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    //updload the image on azure blob storage
                    var imageBlobUrl = await _azureBlobService.UploadImageCompressedAsync(imageUrl);
                    //var imageBlobUrl = await _azureBlobService.UploadImageAsync(imageUrl);
                    aMeal.MealImage = imageBlobUrl;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while generating Meal Image for the meal : {aMeal.mealRecipeBreakdown.NameOfTheMeal}");
                throw new Exception($"Error while generating Meal Image for the meal : {aMeal.mealRecipeBreakdown.NameOfTheMeal}", ex);
            }
        }
    }

}