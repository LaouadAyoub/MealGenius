using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StatusController : Controller
    {
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly UserDbContext _dbContext;

        public StatusController(UserManager<ApplicationUser>  userManager, UserDbContext dbcontext)
        {
            _userManager = userManager;
            _dbContext = dbcontext;
        }


        [HttpGet("GetUserTask")]
        public async Task<IActionResult> GetUserTask()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return Unauthorized();
            }

            var userTask = await _dbContext.Tasks.Where(ud => ud.UserId == user.Id).OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync();

            var userUnputs = await _dbContext.UserInputs.Where(ud => ud.UserId == user.Id).FirstOrDefaultAsync();
            if (userTask == null || userUnputs is null)
            {
                return NotFound("User Task not found");
            }

            var userData = JsonConvert.DeserializeObject<UserData>(userUnputs.UserData);
            // Include the username with the meal plan
            var response = new
            {
                userTaskid = userTask.Id,
                Status = userTask.Status.ToString(),
                UserOutputStatus = userTask.UserOutputStatus.ToString(),
                Imagestatus = userTask.MealsImagesStatus.ToString(),
                userData = userData
            };

            return Ok(response);
        }

        [HttpGet("GetStatus")]
        public async Task<ActionResult<VersionInfoDto>> GetStatus()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return Unauthorized();
            }
            // Initialisation du DTO avec des valeurs par défaut pour éviter de renvoyer null
            var versionInfo = new VersionInfoDto { MealPlanVersion = 0, GroceryListVersion = 0, DashboardVersion = 0 };

            // Obtenez les informations de version pour MealPlan et GroceryList
            var mealPlanInfo = await _dbContext.MealPlans
                .Where(m => m.User == user)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.MealPlanVersion, m.GroceryListVersion, m.MealsImagesVersion })
                .FirstOrDefaultAsync();

            if (mealPlanInfo != null)
            {
                versionInfo.MealPlanVersion = mealPlanInfo.MealPlanVersion;
                versionInfo.GroceryListVersion = mealPlanInfo.GroceryListVersion;
                versionInfo.MealsImagesVersion = mealPlanInfo.MealsImagesVersion;
            }

            // Obtenez les informations de version pour UserDashboard
            var dashboardVersion = await _dbContext.UserDashboards
                .Where(d => d.User == user)
                .Select(d => d.UserDashboardVersion) // Assurez-vous que cette propriété existe dans votre modèle
                .FirstOrDefaultAsync();

            versionInfo.DashboardVersion = dashboardVersion;

            var userTask = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (userTask == null)
            {
                return NotFound("User Task not found");
            }
            versionInfo.UserName = user.UserName;
            TimeSpan MealplanTimeDifference = TimeSpan.FromSeconds(60 * 3) - (DateTime.Now - userTask.CreatedAt);
            versionInfo.MealplanWaitingTime = FormatTimeSpanAsString(MealplanTimeDifference > TimeSpan.Zero ? MealplanTimeDifference : TimeSpan.FromSeconds(5));
        
            TimeSpan GroceryListTimeDifference = TimeSpan.FromSeconds(60 * 4) - (DateTime.Now - userTask.CreatedAt);
            versionInfo.GroceryListWaitingTime = FormatTimeSpanAsString(GroceryListTimeDifference > TimeSpan.Zero ? GroceryListTimeDifference : TimeSpan.FromSeconds(5));


            versionInfo.DashboardWaitingTime = "45 sec";

            if (userTask != null && userTask.DashboardsGenerationExcecutedAt.HasValue)
            {
                // Calculate the time that has passed since the task started executing
                TimeSpan timePassedSinceExecutionStartDashboard = DateTime.UtcNow - userTask.DashboardsGenerationExcecutedAt.Value;
                // Calculate the remaining time by subtracting the time passed from the estimation time
                TimeSpan estimatedWaitingTime = userTask.DashboardGenerationEstimationTime - timePassedSinceExecutionStartDashboard;
                string formattedTime = FormatTimeSpanAsString(estimatedWaitingTime);
                versionInfo.DashboardWaitingTime = formattedTime;
            }
            if (userTask != null && userTask.MealsGenerationExcecutedAt.HasValue)
            {
                TimeSpan timePassedSinceExecutionStartMealPlan = DateTime.UtcNow - userTask.MealsGenerationExcecutedAt.Value;
                TimeSpan estimatedWaitingTime = userTask.MealsGenerationEstimationTime - timePassedSinceExecutionStartMealPlan;
                string formattedTime = FormatTimeSpanAsString(estimatedWaitingTime);
                versionInfo.MealplanWaitingTime = formattedTime;
            }
            if (userTask != null && userTask.GroceryListsGenerationExcecutedAt.HasValue)
            {
                TimeSpan timePassedSinceExecutionStartGroceryList = DateTime.UtcNow - userTask.GroceryListsGenerationExcecutedAt.Value;
                TimeSpan estimatedWaitingTime = userTask.GroceryListGenerationEstimationTime - timePassedSinceExecutionStartGroceryList;
                string formattedTime = FormatTimeSpanAsString(estimatedWaitingTime);
                versionInfo.GroceryListWaitingTime = formattedTime;
            }
            if (userTask != null && userTask.MealsImagesGenerationExcecutedAt.HasValue)
            {
                TimeSpan timePassedSinceExecutionStartListMealsImages = DateTime.UtcNow - userTask.MealsImagesGenerationExcecutedAt.Value;
                TimeSpan estimatedWaitingTime = userTask.MealsGenerationEstimationTime - timePassedSinceExecutionStartListMealsImages;
                string formattedTime = FormatTimeSpanAsString(estimatedWaitingTime);
                versionInfo.MealplanWaitingTime = formattedTime;
            }

            return Ok(versionInfo);
        }


        private static string FormatTimeSpanAsString(TimeSpan timeSpan)
        {
            // Ensure the timeSpan is not negative; if it is, set it to 1 second
            if (timeSpan < TimeSpan.FromSeconds(1))
            {
                timeSpan = TimeSpan.FromSeconds(1);
            }

            // Format the timeSpan based on its duration
            if (timeSpan.TotalMinutes < 1)
            {
                var roundedSeconds = 5 * Math.Ceiling(timeSpan.Seconds / 5.0); // Round up to the nearest multiple of 5

                // If less than 1 minute, display in "seconds" format
                return $"{roundedSeconds} seconds";
            }
            else
            {
                // Display in "XminYsec" format for durations of 1 minute or longer
                return $"{timeSpan.Minutes}min{timeSpan.Seconds}sec";
            }
        }

    }

    public class VersionInfoDto
    {
        public string UserName { get; set; }
        public long DashboardVersion { get; set; }

        public long MealPlanVersion { get; set; }

        public long GroceryListVersion { get; set; }
            
        public long MealsImagesVersion { get; set; }
        public string DashboardWaitingTime { get; set; }
        public string MealplanWaitingTime { get; set; }
        public string GroceryListWaitingTime { get; set; }
    }
}
