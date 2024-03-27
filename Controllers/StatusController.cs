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
        private readonly UserDbContext _dbcontext;

        public StatusController(UserManager<ApplicationUser>  userManager, UserDbContext dbcontext)
        {
            _userManager = userManager;
            _dbcontext = dbcontext;
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

            var userTask = await _dbcontext.Tasks.Where(ud => ud.UserId == user.Id).OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync();

            var userUnputs = await _dbcontext.UserInputs.Where(ud => ud.UserId == user.Id).FirstOrDefaultAsync();
            if (userTask == null || userUnputs is null)
            {
                return NotFound("User Task not found");
            }

            var userData = JsonConvert.DeserializeObject<UserInputsDataModel>(userUnputs.UserData);
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
            var mealPlanInfo = await _dbcontext.MealPlans
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
            var dashboardVersion = await _dbcontext.UserDashboards
                .Where(d => d.User == user)
                .Select(d => d.UserDashboardVersion) // Assurez-vous que cette propriété existe dans votre modèle
                .FirstOrDefaultAsync();

            versionInfo.DashboardVersion = dashboardVersion;

            // Vous pouvez calculer MealplanWaitingTime ici basé sur votre logique d'affaires
            versionInfo.MealplanWaitingTime = "3 min";
            versionInfo.GroceryListWaitingTime = "4 min";
            versionInfo.DashboardWaitingTime = "45 sec";

            //versionInfo.MealPlanVersion = 0;
            //versionInfo.DashboardVersion = 1;
            //versionInfo.MealsImagesVersion = 0;
            //versionInfo.GroceryListVersion = 0;

            return Ok(versionInfo);
        }

    }

            public class VersionInfoDto
        {
            public long DashboardVersion { get; set; }

            public long MealPlanVersion { get; set; }

            public long GroceryListVersion { get; set; }
            
            public long MealsImagesVersion { get; set; }
            public string DashboardWaitingTime { get; set; }
            public string MealplanWaitingTime { get; set; }
            public string GroceryListWaitingTime { get; set; }
        }
}
