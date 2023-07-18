using MealGeniusBackend.Models.ModelGPT;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPI : ControllerBase
    {
        private readonly IOpenAIService _openAIService;

        public MainAPI(IOpenAIService openAIService)
        {
            _openAIService = openAIService;
        }

        //[Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateMeal([FromBody] UserInfos userInfos)
        {
            // Send user information and preferences to OpenAI API and get response
            string chatResponse = await _openAIService.GetMealPlan(userInfos);

            return Ok(chatResponse);
        }


    }
}
