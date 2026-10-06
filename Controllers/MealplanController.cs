using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Mapper;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services.Dashboard;
using MealGeniusBackend.Utilities;
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
    public class MealplanController : Controller
    {
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly UserDbContext _dbcontext;

        public MealplanController(UserManager<ApplicationUser>  userManager, UserDbContext dbcontext)
        {
            _userManager = userManager;
            _dbcontext = dbcontext;
        }


        [HttpGet("GetMealRecipe")]
        public async Task<IActionResult> GetMealPlan()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
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
                return NotFound("Mealplan not found.");
            }

            var userMealPlan = JsonConvert.DeserializeObject<UserMealsRoot>(latestMealPlan.MealPlanJson);
            List<aMealWithoutRecipe> mappedMeals = new List<aMealWithoutRecipe>();

            foreach (var meal in userMealPlan.UserMeals)
            {
                meal.Recipe = "";

            }

            // Include the username with the meal plan
            var response = new
            {
                userName = user.UserName,
                mealPlan = userMealPlan
            };

            return Ok(response);
        }


        // update MealPlan
        [HttpPost("UpdateMealPlan")]
        public async Task<IActionResult> UpdateMealPlan([FromBody] UserMealsRoot mealPlanJson)
        {
            if (mealPlanJson.UserMeals is null || mealPlanJson.UserMeals.Count is < 1 or > 50)
                return BadRequest("Provide between 1 and 50 meals.");
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
            if (user == null)
            {
                return Unauthorized();
            }

            var existingMealPlan = await _dbcontext.MealPlans
                                  .Include(p => p.Task)
                                  .Where(ud => ud.UserId == user.Id)
                                  .OrderByDescending(m => m.CreatedAt)
                                  .FirstOrDefaultAsync();
            if (existingMealPlan == null)
            {
                return NotFound("Mealplan not found.");
            }
            var userNewMealPlan = JsonConvert.SerializeObject(mealPlanJson);

            if (existingMealPlan.Task.Status != UserTaskStatus.Completed)
                return Conflict("Wait for generation to finish before editing meals.");

            existingMealPlan.MealPlanJson = userNewMealPlan;
            existingMealPlan.MealPlanVersion++;
            _dbcontext.MealPlans.Update(existingMealPlan);
            await _dbcontext.SaveChangesAsync();

            return Ok("Meal plan updated successfully.");
        }




        [HttpGet("GetGroceryList")]
        public async Task<IActionResult> GetGroceryList()
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
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
                return NotFound("User groceryList not found.");
            }

            var userGroceryListJson = JsonConvert.DeserializeObject<GroceryCategoriesDetailed>(userGroceryList.GroceryListJson);

            //var groceryItems = _dbContext.GroceryItems.ToList();
            //foreach (var category in userGroceryListJson.GroceryCategories)
            //{
            //    foreach(var item in category.GroceryItems)
            //    {
            //        foreach(var groceryItem in groceryItems)
            //        {
            //            if (item.GroceryItemName == groceryItem.Name)
            //            {
            //                if(!string.IsNullOrEmpty(groceryItem.CompressedImageUrl))
            //                {
            //                    item.GroceryItem_ImageUrl = groceryItem.CompressedImageUrl;
            //                }
            //            }
            //        }
            //    }
            //}


            //var serializerSettings = new JsonSerializerSettings
            //{
            //    ContractResolver = new IgnorePropertiesResolver(new[] { "SimilarNames" })
            //};

            //// Serialize your object with the custom settings
            //var json = JsonConvert.SerializeObject(userGroceryListJson, serializerSettings);

            return Ok(userGroceryListJson);
        }
    
    
        

    }
}
