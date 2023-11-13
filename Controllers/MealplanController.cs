using MealGeniusBackend.DataAcess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealGeniusBackend.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class MealplanController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly UserDbContext _dbcontext;

        public MealplanController(UserManager<IdentityUser> userManager, UserDbContext dbcontext)
        {
            _userManager = userManager;
            _dbcontext = dbcontext;
        }


        [HttpGet("GetMealPlan")]
        public async Task<IActionResult> GetMealPlan()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return Unauthorized();
            }

            // Fetch the lastest user's mealplan
            var latestMealPlan = await _dbcontext.MealPlans
                                         .Where(ud => ud.UserId == user.Id)
                                         .OrderByDescending(m => m.CreatedAt)
                                         .Select(m => new { m.MealPlanJson })
                                         .FirstOrDefaultAsync();
            var username = user.UserName;
            if (latestMealPlan == null)
            {
                return NotFound("User Mealplan not found.");
            }

            // Include the username with the meal plan
            var response = new
            {
                userName = user.UserName,
                mealPlan = latestMealPlan.MealPlanJson
            };

            return Ok(response);
        }

        [HttpGet("GetGroceryList")]
        public async Task<IActionResult> GetGroceryList()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return Unauthorized();
            }

            // Fetch the user's dashboard info
            var userGroceryList = await _dbcontext.MealPlans
                                  .Where(ud => ud.UserId == user.Id)
                                  .OrderByDescending(m => m.CreatedAt)
                                  .Select(m => new { m.GroceryListJson })
                                  .FirstOrDefaultAsync();
            if (userGroceryList == null)
            {
                return NotFound("User dashboard not found.");
            }

            return Ok(userGroceryList);
        }
    }
}
