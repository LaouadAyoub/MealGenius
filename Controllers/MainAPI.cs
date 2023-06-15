using MealGeniusBackend.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPI : ControllerBase
    {
        [HttpPost]
        // http://localhost:5139/api/MainAPI
        public IActionResult CreateMeal([FromBody] MealModel meal)
        {
            // Print the received form values to the console
            System.Console.WriteLine("Cuisine Type: " + meal.CuisineType);
            System.Console.WriteLine("Height: " + meal.Height);
            System.Console.WriteLine("Weight: " + meal.Weight);
            System.Console.WriteLine("Objective: " + meal.Objective);

            // Return a  success message or any other desired response
            return Ok("Meal created successfully!");
        }
    }
}
