using MealGeniusBackend.Models.Model2ndResponse;
using MealGeniusBackend.Models.ModelGPT;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NLog; // add this line

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPIController : ControllerBase
    {
        private readonly IOpenAIService _openAIService;
        private static Logger logger = LogManager.GetCurrentClassLogger(); // add this line

        public MainAPIController(IOpenAIService openAIService)
        {
            _openAIService = openAIService;
        }

        [HttpPost("CreateMeal")]
        //[Authorize]
        public async Task<IActionResult> CreateMeal([FromBody] UserInfos userInfos)
        {
            logger.Info("CreateMeal method called."); // add this line

            try
            {
                // Send user information and preferences to OpenAI API and get response
                var chatResponse = await _openAIService.GetMealPlan(userInfos);

                logger.Info("Meal plan retrieved successfully."); // add this line
                return Ok(chatResponse);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while creating meal plan.");
                return StatusCode(500, "An error occurred while creating the meal plan.");
            }
        }
    }
}
