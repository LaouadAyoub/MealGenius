using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models.ModelGPT;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static MealGeniusBackend.Controllers.MainAPIController;

namespace MealGeniusBackend.Services
{
    public interface IUserDashboardService
    {
        void GenerateUserDashboard(UserTaskDTO userTaskDTO);
    }
    public class UserDashboardService : IUserDashboardService
    {
        private readonly UserDbContext _dbContext;
        private readonly ILogger<UserDashboardService> _logger;

        public UserDashboardService(UserDbContext userDbContext, ILogger<UserDashboardService> logger)
        {
            _dbContext = userDbContext;
            _logger = logger;

        }
        public void GenerateUserDashboard(UserTaskDTO userTaskDTO)
        {
            var existingDashboard = _dbContext.UserDashboards
                .SingleOrDefault(dashboard => dashboard.TaskId == userTaskDTO.Id);
            _logger.LogInformation($"UserDashboard already exists with : {userTaskDTO.Id}");
            if (existingDashboard != null)
            {
                return;
            }

            // Logic for generating a user dashboard
            var userInput = _dbContext.UserInputs
                        .Where(u => u.TaskId == userTaskDTO.Id)
                        .FirstOrDefault();
            if(userInput is null || userInput.UserData is null)
            {
                _logger.LogError("UserInput not found");
                return;
            }

            #region UpdateTaskStatus
            //var userTask =  _dbContext.Tasks.Where(t => t.Id == userTaskDTO.Id).FirstOrDefaultAsync();
            //// Update the task status to InProgress directly
            //userTask.Status = UserTaskStatus.InProgress;
            //_dbContext.Tasks.Update(userTask);
            //// The Update call is redundant if you're tracking the entity, so it can be omitted.
            //// _dbContext.Tasks.Update(userTask);
            //await _dbContext.SaveChangesAsync();
            //_logger.LogInformation("UserTask with ID {Id} status updated to InProgress", userTaskDTO.Id);

            //if (userTask == null)
            //{
            //    _logger.LogError("UserTask with ID {Id} not found", userTaskDTO.Id);
            //    return;
            //} 
            #endregion
            //TODO : should i really serialise/deserialise ?
            UserInputDataModel userDataModel = JsonConvert.DeserializeObject<UserInputDataModel>(userInput.UserData);

            var summarySection = @"Welcome Ayoub, your custom weekly meal plan is ready. Aim for 3500 calories per day to reach your goal weight of 75 in 12 weeks. Let's do this together!";
            var bmrInitialContent = @"Your BMR or Basic Metabolic Rate is [2,500] calories/day";
            var bmrExpandedText = @"This figure represents the number of calories your body requires to maintain basic functions at rest. We calculated this using the Mifflin-St Jeor Equation, factoring in your age, weight, height, and gender.";
            var caloricNeedsInitialContent = @"To achieve your goal weight of [75 kg] in [12] weeks, you need [X] calories/day.";

            var caloricNeedsExpandedText = @"
**We've tailored your caloric needs based on several factors.** Your activity level increases your BMR by **[X]%**, taking you to **[Y] calories/day**. Your goal weight of **[75 kg]** requires an additional **[Z] calories/day**. In total, you need **[X+Y+Z] calories/day** to reach your goal weight in 12 weeks.

## Macronutrient Breakdown:
*Based on your personalized plan, your daily macronutrients are as follows:*
- **Proteins:** [A] grams 🥩
- **Carbs:** [B] grams 🍞
- **Fats:** [C] grams 🥑
                                            ";

            string userDetails = @"Hey there, champion! 🏆

So, you've spilled the tea and we've been listening. Here's what we've cooked up just for you!

## On the Road to Wellness 🛣️🌱
At the ripe age of 25, you're all about that healthy life. We're making sure your meal plan is as **vegetarian** as a summer music festival.

## You're a Foodie, Aren't Ya? 🍝
Italian cuisine is your jam? Say less. We're avoiding your **no-go zones** like certain allergies.

## Vibes Check 😎
We're aiming for you to feel more like Iron Man and less like Tony Stark after a bender. You in?

## Sorry, We Don't Serve That Here 🚫
Heard you don't vibe with **[specific foods]**. Noted and avoided!

## Gym or Home Yoga? 🏋️‍♀️
You're into staying active, which is dope. We're on it.

## What's the Endgame? 🎯
We're prepping meals that'll help you score those **[specific goals]** and have you saying, 'Delizioso!'";


            // 2. Generate UserDashboard info (Dummy data for now)
            UserDashboard newDashboard = new UserDashboard
            {
                Id = Guid.NewGuid(),
                UserId = userTaskDTO.UserId,
                TaskId = userTaskDTO.Id,
                SummarySection = summarySection,
                BmrInitialContent = bmrInitialContent,
                BmrExpandedText = bmrExpandedText,
                CaloricNeedsInitialContent = caloricNeedsInitialContent,
                CaloricNeedsExpandedText = caloricNeedsExpandedText,
                UserDetails = userDetails
            };

            // 3. Store info in UserDashboards table
            _dbContext.UserDashboards.Add(newDashboard);
            _dbContext.SaveChanges();

            _logger.LogInformation($"UserDashboard generated for UserId: {userTaskDTO.UserId}");
        }
    }



}