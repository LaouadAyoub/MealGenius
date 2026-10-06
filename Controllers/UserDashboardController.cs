using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Controllers
{
    [Authorize(Policy = "PaidUser")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserDbContext _dbcontext;

        public UserDashboardController(UserManager<ApplicationUser> userManager, UserDbContext dbcontext)
        {
            _userManager = userManager;
            _dbcontext = dbcontext;
        }

        [HttpGet("GetUserDashboard")]
        public async Task<IActionResult> GetUserDashboard()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return Unauthorized();
            }

            // Fetch the user's dashboard info
            var userDashboard = await _dbcontext.UserDashboards
                                .Where(ud => ud.UserId == user.Id)
                                .OrderByDescending(ud => ud.Task.CreatedAt)
                                .FirstOrDefaultAsync();

            if (userDashboard == null)
            {
                return NotFound("User dashboard not found.");
            }

            object userJsonUserKeyInfos;
            try
            {
                userJsonUserKeyInfos = JsonConvert.DeserializeObject<NutritionData>(userDashboard.JsonUserKeyInfos);
            }
            catch (JsonException)
            {
                // If deserialization fails, revert to the original JSON string
                userJsonUserKeyInfos = userDashboard.JsonUserKeyInfos;
            }

            return Ok(new
            {
                FirstName = user.FirstName,
                userGoalsGuide = userDashboard.UserGoalsGuide,
                MacroTargets = userDashboard.MacroTargets,
                MicroGuide = userDashboard.MicroGuide,
                WaterIntake = userDashboard.WaterIntake,
                // Return either the deserialized object or the original JSON string
                JsonUserKeyInfos = userJsonUserKeyInfos
            });

        }


        public class NutritionData
        {
            public string BMR { get; set; }
            public string TotalCaloricNeed { get; set; }
            public string WaterIntake { get; set; }
            public List<string> EssentialMicronutrients { get; set; }
            public MacronutrientRatio MacronutrientRatio { get; set; }
        }

        public class MacronutrientRatio
        {
            public Macronutrient Protein { get; set; }
            public Macronutrient Fats { get; set; }
            public Macronutrient Carbohydrates { get; set; }
        }

        public class Macronutrient
        {
            public string Grams { get; set; }
            public string Percentage { get; set; }
        }
    }
}
