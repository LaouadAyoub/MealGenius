using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Services;
using MealGeniusBackend.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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
                return NotFound("User Mealplan not found.");
            }

            var userMealPlan = JsonConvert.DeserializeObject<UserMealsRoot>(latestMealPlan.MealPlanJson);
            // Include the username with the meal plan
            var response = new
            {
                userName = user.UserName,
                mealPlan = userMealPlan
            };

            return Ok(response);
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

            //var groceryItems = _dbcontext.GroceryItems.ToList();
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
