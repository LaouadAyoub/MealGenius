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
    public class UserDashboardController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly UserDbContext _dbcontext;

        public UserDashboardController(UserManager<IdentityUser> userManager, UserDbContext dbcontext)
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
                                .FirstOrDefaultAsync();

            if (userDashboard == null)
            {
                return NotFound("User dashboard not found.");
            }

            return Ok(new
            {
                UserDetails = userDashboard.UserDetails,
                SummarySection = userDashboard.SummarySection,
                BmrInitialContent = userDashboard.BmrInitialContent,
                BmrExpandedText = userDashboard.BmrExpandedText,
                CaloricNeedsInitialContent = userDashboard.CaloricNeedsInitialContent,
                CaloricNeedsExpandedText = userDashboard.CaloricNeedsExpandedText
            });
        }
    }
}
