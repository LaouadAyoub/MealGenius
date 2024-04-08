using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MealGeniusBackend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbackController : ControllerBase
    {
        private readonly UserDbContext _dbcontext;
        private readonly UserManager<ApplicationUser> _userManager;


        public FeedbackController(UserManager<ApplicationUser> userManager, UserDbContext dbcontext)
        {
            _dbcontext = dbcontext;
            _userManager = userManager;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PostFeedback([FromBody] FeedbackModel feedback)
        {
            // Get the current authenticated user
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return Unauthorized();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newFeedback = new Feedback
            {
                UserId = user.Id,
                Rating = feedback.Rating,
                Like = feedback.Like,
                Improvement = feedback.Improvement
            };
            _dbcontext.Feedbacks.Add(newFeedback);
            await _dbcontext.SaveChangesAsync();

            return Ok(); // Returns HTTP 200 without content. You can also return CreatedAtAction if you prefer.
        }
    }
    public class FeedbackModel
    {
        public int Rating { get; set; }
        public string Like { get; set; }
        public string Improvement { get; set; }
    }

}
