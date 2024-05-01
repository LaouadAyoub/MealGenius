using MealGeniusBackend.DataAcess;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenAI_API.Chat;
using OpenAI_API;
using static MealGeniusBackend.Controllers.MainAPIController;
using Newtonsoft.Json.Linq;
using MealGeniusBackend.Models;

namespace MealGeniusBackend.Services.Dashboard
{
    public interface IUserDashboardService
    {
        Task GenerateUserDashboard(UserTaskDTO userTaskDTO);
    }
    public class UserDashboardService : IUserDashboardService
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<UserDashboardService> _logger;
        private readonly IOpenAIService _openAIService;

        public UserDashboardService(UserDbContext userDbContext, ILogger<UserDashboardService> logger, IOpenAIService openAIService)
        {
            _dbContext = userDbContext;
            _logger = logger;
            _openAIService = openAIService;
        }
        public async Task GenerateUserDashboard(UserTaskDTO userTaskDTO)
        {

            var userTask = _dbContext.Tasks.Where(u => u.Id == userTaskDTO.Id).FirstOrDefault();   
            try
            {

                userTask.DashboardsGenerationExcecutedAt = DateTime.UtcNow;

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


                var existingDashboard = _dbContext.UserDashboards.SingleOrDefault(dashboard => dashboard.TaskId == userTaskDTO.Id);


                if (existingDashboard != null)
                {
                    if(userTaskDTO.Status == UserTaskStatus.TobeRetried)
                    {
                        _dbContext.UserDashboards.Remove(existingDashboard);
                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Dashboard already exists for this task");
                        return;
                    }
                }
                var timer = new ServiceTaskTimer("UserDashboardService", "Start the generation of the dashboard");
                timer.Start();
                var dashboardInfos = await GetDashboardInfo(userInput.UserData);
                timer.StopAndLog();
                UserDashboard newDashboard = new UserDashboard
                {
                    Id = Guid.NewGuid(),
                    UserId = userTaskDTO.UserId,
                    TaskId = userTaskDTO.Id,
                    UserGoalsGuide = dashboardInfos.UserGoalsGuide,
                    MacroTargets = dashboardInfos.MacroTargets,
                    MicroGuide = dashboardInfos.MicroGuide,
                    WaterIntake = dashboardInfos.WaterIntake,
                    JsonUserKeyInfos = dashboardInfos.JsonReponse,
                    UserDashboardVersion = 1
                };

                // 3. Store info in UserDashboards table
                _dbContext.UserDashboards.Add(newDashboard);


                userTask.UserOutputStatus = UserOutputStatus.DashboardCompleted;

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"UserDashboard and Task updated for UserId: {userTaskDTO.UserId}");
            }
            catch (Exception ex)
            {
                try
                {
                    userTask.Status = UserTaskStatus.Failed;
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex2)
                {
                    _logger.LogError(ex2, "Error while updating user task status to failed");
                    throw;
                }
                _logger.LogError(ex, "Error while generating user dashboard");
                throw;
            }
        }

        private async Task<(string MacroTargets, string JsonReponse, string MicroGuide, string WaterIntake, string UserGoalsGuide)> GetDashboardInfo(string UserInputsJson)
        {
            try
            {
                // ChatMacroTargets section

                //CaloricNeedsCalculator.CalculateBMR(age, weight, height);

                string systemPrompt = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
                                It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
                                The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

                                MealGenius is used globally, so please use simple, clear English, avoiding slang or region-specific terms.
                                You can use emojis to add engagement and fun, but only where appropriate – do not overuse them.

                                User information relevant informations is provided in the following JSON format.
                                {UserInputsJson}";


                string userMacroTargetsprompt = @"
This is the macrotarget section of the user. Craft an engaging markdown text that explains the Macro targets of the user, starting from the Basal Metabolic Rate (BMR) and daily caloric needs and macronutrients distribution of a user in a friendly, conversational tone, as if a nutritionist is speaking directly to them. The text should be mainly educative and informative while being fun and engaging, and provide value in an easy-to-understand format. Include these key sections:

# Introduction to Your Macronutritional Targets
Begin with a fun and welcoming introduction that sets the stage for discovering the user's personalized macronutrient goals.setting the tone for an educational journey into understanding personal energy needs.

## Understanding Your Macronutrition Needs
Content: Provide an overview of macronutrients and their importance in diet, gearing the explanation towards how they will help the user meet their specific health goals.


### Understanding Your Basal Metabolic Rate (BMR)

Begin with a brief, friendly introduction explaining the importance of BMR in understanding calorie needs.

#### BMR Calculation

Using a creative header, akin to ""Decoding Your Body's Energy Needs,"" explain the Mifflin-St Jeor equation simply and how it calculates BMR based on user details. Perform the calculation and display the results engagingly.
Note for GPT : It's very important and crucial that you the calculations for the users and display the RESULTS.

#### Your Activity Levels and Caloric Needs
Adjust caloric needs based on the user's activity level. Detail each activity level with its own subsection:

Data for GPT : the user had 5 options :
[Always on the Go] - 🌪️: High activity factor
[Regularly Active] - 🚴‍♀️: Moderate to high activity factor
[Occasionally Active] - ⚽: Moderate activity factor
[Seldom Active] - 🛋️: Low to moderate activity factor
[Rarely Active] - 🧘: Low activity factor

Content: calculate the adjusted caloric needs based on the user's chosen level. Provide the actual caloric value needed for maintenance, considering their activity level. Emphasize the user's chosen activity level and its corresponding factor in a straightforward manner.

### Reaching Your Goal Weight
Under a header like ""Charting the Course to Your Weight Goal,"" explain the impact of caloric intake on weight management. 

Provide tailored examples of caloric surplus or deficit for weight gain or loss, matching the user's pace and goal.
Content: Explain how adjusting caloric intake impacts weight gain or loss. Provide a clear example, like ""Consuming X calories above/below maintenance may lead to a gain/loss of 0.25 to 0.5 per week and/or per mounth"" Tailor this part to the user's goal (gain, loss, or maintenance) and desired pace. 
Note for GPT : It's very important and crucial that you display the calculations for the users and display the RESULTS. It's also important to specify that it's all about balance and flexibility and to listen to your own body.

### Balancing Your Macronutrients
Under a header similar to ""Fine-Tuning Your Fuel,"" offer a macronutrient distribution plan. Give specific percentages and gram-per-day recommendations for proteins, fats, and carbohydrates, and justify why this balance suits the user's objectives.
Note for GPT : It's very important and crucial that you the calculations for the users and display the RESULTS.

# Conclusion (you should give it a creative title)
In conclusion, remind the user that these estimations are a starting point and listening to their body for adjustments is essential. Summarize the key takeaways, making sure to bold the most critical data for emphasis. 
And finally Reassure them that MealGenius is here to support them on their journey to optimal health.

Use emojis sparingly to keep things light and engaging. Ensure the layout is user-friendly, with bullet points for digestible reading. Bold conclusion and important informations";


                // Micro Guide section
                string userMicroGuidePrompt = @"
This is the micronutrition guide section for the user. Your task is to generate an engaging and informative markdown text that helps the user understand the world of micronutrients, specifically vitamins and minerals, as related to their health and nutritional goals. Write in a friendly, conversational tone with a strong educational underpinning, making the content digestible and fun. Remember to use simple language and avoid medical jargon while keepint it informative. Include the following key sections:

# Welcome to Your Micronutrient Map!
Start with an exciting and friendly introduction that invites the user to learn about the vital role vitamins and minerals play in their diet, overall health, and specific goals.

## Exploring the Micronutrient Universe
Content: Provide a bird's eye view of what micronutrients are, including their role in the body's functioning, emphasizing their importance in helping the user achieve their health objectives. 

### Vital Vitamins
Under a section titled with a title similar to ""Unlocking the Power of Vitamins,"" provide a detailed list of minimun 3 essential vitamins for the user. For each vitamin mentioned, include the following:

- **Vitamin Benefits**:
  - Describe the health benefits in a way that is both technical and approachable, integrating how it aligns with the user's health goals. Mention potential symptoms of deficiency that the user can be aware of.
  
- **Vitamin Sources**:
  - List the foods and dietary sources where one can find these vitamins. This should be practical and easy for the user to incorporate into their diet.

### Mighty Minerals 
Create a section titled ""Minerals: Your Body's Building Blocks"" and mirror the vitamin section's format, presenting a range of necessary minerals. list of minimun 3 essential Minerals for the user ,Each mineral should have:

- **Mineral Benefits**:
  - Explain the specific health benefits and how they support the user's personalized nutritional needs. Be sure to highlight the consequences of not getting enough of these minerals.
  
- **Mineral Sources**:
  - Enumerate dietary sources rich in these minerals to help the user add them to their meal plans easily.

# Key Takeaways: Your Nutrient Treasure Chest 
In conclusion, provide a concise summary of the essential points from the guide. Remind the user that while micronutrients might be tiny, their impact on health is huge! Highlight the most crucial information and include the following:

- A reminder that this is a personalized guide, and they should adjust their intake based on their body's response.
- Encourage the user to vary their diet to cover the spectrum of vitamins and minerals discussed.
- Reassure them that MealGenius is here to support them on their journey to optimal health.

Use emojis sparingly throughout to maintain a light-hearted feel. Organize the content with bullet points for easy reading and bold the most important information to draw the user's attention.

Note for GPT: It is important to keep the layout user-friendly and provide value in a format that is engaging and easy to understand. The information should be tailored to the user's needs and goals, with practical advice on dietary sources.
";


                // water intake section
                string userWaterIntakePrompt = @"
Your task is to create a guide that not only informs users about the significance of hydration but also provides them with personalized insights into how much water they should be drinking daily, taking into account their activity levels, sleep patterns, and the impact of electrolytes on bodily functions.

Write in a tone that is friendly, supportive, and professional, as if speaking directly to someone seeking advice from a hydration consultant. The guide's content should be both informative and enjoyable, formatted to engage a wide-ranging audience specifically ADHD people and address their specific hydration queries. Each section should begin with a simple and descriptive title, followed by content that adheres to the guidelines given below.

# Welcome to Hydration guide (you should give it a creative title)
start by adressing the user by their name,  welcoming them to the hydration guide of MealGenius. Provide a warm introduction that conveys the critical importance of hydration to the user's health and wellness journey.

## Importance of Staying Hydrated  
Delve into why hydration is vital for the body and mind, highlighting the potential negative effects if not adequately hydrated.

### Daily Water Intake: How Much is Right for You?  
Clarify the general guidelines for water intake, focusing on presenting calculations that illustrate the user's daily water needs in a bold and clear manner. Use ""half an ounce of water for a pound of body weight"" and ""32.6 milliliters of water for every kilogram of body weight"" to compute the user's recommended water intake and present the results directly. 
Present this section with line breaks and bullet points to ensure that information is organized and easily digestible. 
Use **bold** text for important takeaways.

#### Adjusting Water Intake for Your Lifestyle  
Discuss how various lifestyle factors, such as physical activity levels and environmental conditions, influence daily water needs. Offer guidance on increasing hydration in response to:

- Higher levels of physical activity, which can encompass both energy expenditures like exercise and the resulting sweat production.
- Environmental factors, including exposure to hot, humid, or dry conditions that may lead to increased fluid loss.

- Tell the users that it's important to listen to your body and adjust your body intake
Present this information in a structured format, employing bullet points and clear spacing for enhanced readability and user engagement.

#### Hydration vs. Sleep: Finding the Balance  
Investigate the relationship between evening hydration and sleep quality, offering practical steps to minimize nighttime disruptions while maintaining proper hydration levels.

#### Recognizing Dehydration and Overhydration  
Inform users about the indicators of both dehydration and overhydration, stressing the significance of understanding and responding to their body's hydration signals. Provide guidance on identifying symptoms and how to adjust water intake for proper hydration balance. Use clear, simple indicators and corrective measures for each condition to facilitate user understanding and action.

### Electrolytes Explained  
Provide a complete dtailed explanation about : what are electrolytes and explain The relationship between electrolytes and hydration and then their significance to overall health. Address how to maintain a good balance of electrolytes and the potential issues related to imbalances.

### Hydrating Through Food and Drinks
Highlight how different foods and beverages (tea, coffe, soups, etcc..) contribute to overall hydration, and provide a list of hydrating foods and liquids, using bullet points for easy reading.


# (conclusion) Building Your Hydration Strategy  
Conclude the guide by reassuring users that these guidelines offer a foundation upon which they can build and tailor their hydration practices. Emphasize the most crucial points in bold and encourage users to listen to their bodies and adjust fluid intake as needed. 

Note to AI: Present this guide with line breaks and bullet points to ensure that information is organized and easily digestible. Use simple language that can be easily understood by a global audience. The aim is to provide value in a format that is engaging, readable, and centered on the user's individual needs and goals. The finished guide should thoroughly instruct the user in a clear, concise, and friendly manner, fostering an understanding of effective hydration practices.";

                // user goals guide
                var UserGoalsGuideUserPrompt = @"
You're tasked with crafting a comprehensive and personalized guide that helps the user navigate through their multiple nutritional goals. 
This guide should serve as a roadmap to support the user in making informed decisions for a balanced and healthy lifestyle. Adopt a friendly and professional tone while being fun an enjoyable, evoking the sense of a trusted nutritionist speaking directly to the user. T
The guide should be engaging, encouraging, and straightforward, providing concise and actionable information catered to the specific goals of the user. Each goal should be discussed in its dedicated section, with simple and clear titles summarizing the focus of each part and a short description directing your writing approach.

The presentation should be user-friendly, using bullet points and line breaks for easy comprehension, and emphasizing important information in **bold** for visibility.

# Setting the Stage for Nutritional Success (you should give it a creative title) 
Create an introductory section that warmly welcomes the user to the MealGenius and to their personalized nutrition journey and sets a positive tone for the commitment they're making towards their health.

## Goal One: [Title Reflecting User’s First Goal]  
Description: Address the user’s first nutritional goal, giving an overview of its importance and how to approach it. Detail specific strategies, dietary adjustments, or habits that can help achieve this goal, ensuring the content is broken down into bullet points for clarity.

## Goal Two: [Title Reflecting User’s Second Goal]  
Description: Tackle the second nutritional goal by outlining actionable steps the user can follow. Provide relevant nutritional information and tips that align with the achievement of this goal, presented in a user-friendly format.

## Goal Three: [Title Reflecting User’s Third Goal]  
Description: Discuss the third nutritional goal, offering guidance and clear recommendations that cater to this specific aim. Remember to make the content accessible and well-structured for ease of understanding.

## Goal Four, Goal Five and so on (if they exist)

# Conclusion: Your Path to Nutritional Mastery  
Conclude with a section that brings together all the user's goals, reinforcing the importance of consistency and flexibility in their nutritional journey. Highlight key takeaways and motivate the user to stay focused on their path, emphasizing MealGenius’s supportive role in their endeavor.

Note to AI: The final guide should be neatly organized, with line breaks and bullet points where appropriate, to make the information digestible and actionable. Utilize simple yet precise language to convey the message effectively to users worldwide. The aim is to deliver a readable, inviting, and instructive guide that empowers the user to pursue and achieve their individual nutritional objectives with confidence.
";

                //var macroTargetsTask =  _openAIService.GetResponseAsync(systemPrompt, userMacroTargetsprompt, OpenAI_API.Models.Model.GPT4, 5500);
                //var microGuideTask =  _openAIService.GetResponseAsync(systemPrompt, userMicroGuidePrompt, OpenAI_API.Models.Model.GPT4, 5500);
                //var waterIntakeTask =  _openAIService.GetResponseAsync(systemPrompt, userWaterIntakePrompt, OpenAI_API.Models.Model.GPT4, 6500);
                //var userGoalsTask =  _openAIService.GetResponseAsync(systemPrompt, UserGoalsGuideUserPrompt, OpenAI_API.Models.Model.GPT4, 5500);

                var macroTargetsTask = _openAIService.GetResponseAsync(systemPrompt, userMacroTargetsprompt, OpenAI_API.Models.Model.GPT4, 5800);
                var microGuideTask = _openAIService.GetResponseAsync(systemPrompt, userMicroGuidePrompt, OpenAI_API.Models.Model.GPT4, 6000);
                var waterIntakeTask = _openAIService.GetResponseAsync(systemPrompt, userWaterIntakePrompt, OpenAI_API.Models.Model.GPT4, 5800);
                var userGoalsTask = _openAIService.GetResponseAsync(systemPrompt, UserGoalsGuideUserPrompt, OpenAI_API.Models.Model.ChatGPTTurbo, 4096);

                await Task.WhenAll(macroTargetsTask, microGuideTask, waterIntakeTask, userGoalsTask);


                // Récupérez les résultats
                string macroTargetsResponse = await macroTargetsTask;
                string microGuideResponse = await microGuideTask;
                string waterIntakeResponse = await waterIntakeTask;
                string userGoalsResponse = await userGoalsTask;

                _logger.LogInformation("Dashboard Responses generated successfully.");


                // user dashboard json generation
                var UserDashboard_JsonExamplePath = "JsonFiles/UserDashboard_JsonExample.json";

                string userDashboard_JsonExample = File.ReadAllText(UserDashboard_JsonExamplePath);

                string systemPromptJson = $@"You are an AI assistant for MealGenius, an app designed for personalized nutrition and meal planning.
 
It focuses on setting nutritional goals and providing tailored meal plans based on user data. 
The app caters to a diverse audience, including individuals with ADHD, and emphasizes informative educative content while being friendly, fun.

your role will be to generate a JSON structure that encapsulates the user's nutritional goals and needs.

the Json should follow exactly the structure of the example below, with only the actual values changing based on the user's data. the json will be deserialised into a C# object, so make sure the keys are valid C# property names.
please find the example json below:
{userDashboard_JsonExample}
";

                var userDashboardFilePath = "PromptFiles/DashboardUserPromptJson.txt";
                string userDashboardFileContent = File.ReadAllText(userDashboardFilePath);

                // Replace placeholders with actual content
                string userPromptJson = userDashboardFileContent
                    .Replace("{MacroTargetsResponse}", macroTargetsResponse)
                    .Replace("{MicroGuideResponse}", microGuideResponse)
                    .Replace("{WaterIntakeResponse}", waterIntakeResponse)
                    .Replace("{userDashboard_JsonExample}", userDashboard_JsonExample);

                //                string userPromptJson = @$"
                //    Based on this {{Markdown text}} detailing the user's Basal Metabolic Rate (BMR), daily caloric needs, water intake, and essential micronutrients, 
                //    generate a JSON structure that encapsulates this data. 
                //    The JSON should include the BMR, total caloric need, macronutrient ratio (protein, fats, and carbohydrates) in both grams and percentages, 
                //    recommended water intake, and a list of essential micronutrients. 
                //    Use this example JSON structure as a guide, ensuring that the key values are consistent, 
                //    with only the actual values changing based on the user's data.

                //the Json should follow exactly the structure of the example below, with only the actual values changing based on the user's data. the json will be deserialised into a C# object, so make sure the keys are valid C# property names.
                //please find the example json below:
                //{userDashboard_JsonExample}
                //    {{Markdown text : {macroTargetsResponse} + {microGuideResponse} + {waterIntakeResponse}}}
                //";

                var JsonUserKeyInfos = await _openAIService.GenerateJsonBasedOnPromptResponseAsync(systemPromptJson, userPromptJson, 600);


                return (MacroTargets: macroTargetsResponse, JsonReponse: JsonUserKeyInfos, MicroGuide: microGuideResponse, WaterIntake: waterIntakeResponse, UserGoalsGuide: userGoalsResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while generating user dashboard JSON: {Message}", ex.Message);
                throw;
            }
        }

    }



}