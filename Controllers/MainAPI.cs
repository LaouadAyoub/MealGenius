using MealGeniusBackend.ModelGPT;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using System;
using System.Text.Json;
using Newtonsoft.Json;
using MealGeniusBackend.Services;

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

        [HttpPost]
        public async Task<IActionResult> CreateMeal([FromBody] UserInfos userInfos)
        {
            // Send user information and preferences to OpenAI API and get response
            string chatResponse = await _openAIService.GetMealPlan(userInfos);

            string chatResponse1 = "Here is the response :\nHere is the requested JSON object:\n\n```\n{\n  \"startFlag\": \"#TheGenerationHasStarted\",\n  \"Monday\": {\n    \"Breakfast\": \"Egg and Spinach Breakfast Sandwich\",\n    \"Lunch\": \"Chicken and Pasta Salad\",\n    \"Dinner\": \"Italian Stuffed Peppers\",\n    \"Snack\": \"Apple Slices with Peanut Butter\"\n  },\n  \"Tuesday\": {\n    \"Breakfast\": \"Blueberry Oatmeal\",\n    \"Lunch\": \"Caprese Salad with Grilled Chicken\",\n    \"Dinner\": \"Chicken Parmesan with Zucchini Noodles\",\n    \"Snack\": \"Greek Yogurt with Berries\"\n  },\n  \"Wednesday\": {\n    \"Breakfast\": \"Avocado Toast with Poached Eggs\",\n    \"Lunch\": \"Pesto Chicken and Tomato Skewers\",\n    \"Dinner\": \"Spaghetti Squash with Meat Sauce\",\n    \"Snack\": \"Carrots and Hummus\"\n  },\n  \"Thursday\": {\n    \"Breakfast\": \"Banana and Peanut Butter Smoothie\",\n    \"Lunch\": \"Italian Chicken and Vegetable Soup\",\n    \"Dinner\": \"Baked Lemon Chicken with Roasted Vegetables\",\n    \"Snack\": \"String Cheese and Grapes\"\n  },\n  \"Friday\": {\n    \"Breakfast\": \"Yogurt and Granola Parfait\",\n    \"Lunch\": \"Chicken Caesar Wrap\",\n    \"Dinner\": \"Zucchini and Chicken Meatballs with Marinara Sauce\",\n    \"Snack\": \"Trail Mix\"\n  },\n  \"Saturday\": {\n    \"Breakfast\": \"Spinach and Feta Omelette\",\n    \"Lunch\": \"Italian Chicken and Rice Bowl\",\n    \"Dinner\": \"Grilled Chicken with Tomato and Basil Salad\",\n    \"Snack\": \"Cottage Cheese with Pineapple\"\n  },\n  \"Sunday\": {\n    \"Breakfast\": \"Whole Wheat Pancakes with Blueberries\",\n    \"Lunch\": \"Italian Chicken and Vegetable Skillet\",\n    \"Dinner\": \"Baked Cod with Lemon and Garlic\",\n    \"Snack\": \"Almond Butter and Apple Slices\"\n  },\n  \"endFlag\": \"#TheGenerationIsFinished\"\n}\n```";

            // Extract JSON from the response string
            //var var = _jsonExtractorService.ExtractMealPlanFromResponse(chatResponse);

            // Return the final response as JSON
            return Ok(chatResponse);
        }


    }
}
