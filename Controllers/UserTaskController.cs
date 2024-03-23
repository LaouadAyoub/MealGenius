using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
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
    [Route("api/[controller]")]
    [ApiController]
    public class UserTaskController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly UserDbContext _dbcontext;

        public UserTaskController(UserManager<IdentityUser> userManager, UserDbContext dbcontext)
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
    }
}
