using MealGeniusBackend.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using System;

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPI : ControllerBase
    {
        private readonly OpenAIAPI _openAiApi;

        public MainAPI(OpenAIAPI openAiApi)
        {
            _openAiApi = openAiApi;
        }

        //[HttpPost]
        //// http://localhost:5139/api/MainAPI
        //public async Task<IActionResult> CreateMeal([FromBody] MealModel meal)
        //{
        //    // add a comment
        //    // Print the received form values to the console
        //    System.Console.WriteLine("Cuisine Type: " + meal.CuisineType);
        //    System.Console.WriteLine("Height: " + meal.Height);
        //    System.Console.WriteLine("Weight: " + meal.Weight);
        //    System.Console.WriteLine("Objective: " + meal.Objective);

        //    var apiKey = "REDACTED";

        //    var api = new OpenAI_API.OpenAIAPI(apiKey);
        //    var chat = api.Chat.CreateConversation();
        //    chat.RequestParameters.MaxTokens = 500;
        //    chat.RequestParameters.Temperature = 0.2;
        //    chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;

        //    chat.AppendSystemMessage("You are a helpful assistant who can create a weekly meal plan based on provided information.");
        //    chat.AppendUserInput($"Create a meal plan for a person with the following details: Cuisine Type: {meal.CuisineType}, Height: {meal.Height}, Weight: {meal.Weight}, Objective: {meal.Objective} weoght");

        //    string result = "";
        //    int streamParts = 0;

        //    await foreach (var streamResultPart in chat.StreamResponseEnumerableFromChatbotAsync())
        //    {
        //        result += streamResultPart;
        //        streamParts++;
        //    }

        //    // Log the result
        //    Console.WriteLine(result);

        //    // Process the generated response and return it
        //    // Example: Return the response as the meal plan
        //    var mealPlan = result;

        //    // Return a success message or any other desired response
        //    //return Ok("Meal created successfully!");
        //    yield return Ok(mealPlan);
        //}

        [HttpPost]
    public async Task CreateMeal([FromBody] MealModel meal)
    {
        // add a comment
        // Print the received form values to the console
        System.Console.WriteLine("Cuisine Type: " + meal.CuisineType);
        System.Console.WriteLine("Height: " + meal.Height);
        System.Console.WriteLine("Weight: " + meal.Weight);
        System.Console.WriteLine("Objective: " + meal.Objective);

        var apiKey = "REDACTED";

        var api = new OpenAI_API.OpenAIAPI(apiKey);
        var chat = api.Chat.CreateConversation();
        chat.RequestParameters.MaxTokens = 500;
        chat.RequestParameters.Temperature = 0.2;
        chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;

        chat.AppendSystemMessage("You are a helpful assistant who can create a weekly meal plan based on provided information.");
        chat.AppendUserInput($"Create a meal plan for a person with the following details: Cuisine Type: {meal.CuisineType}, Height: {meal.Height}, Weight: {meal.Weight}, Objective: {meal.Objective} weight");

        Response.Headers.Add("Content-Type", "text/plain");
        var streamWriter = new StreamWriter(Response.Body);

        // Simulate long-running task
        await foreach (var streamResultPart in chat.StreamResponseEnumerableFromChatbotAsync())
        {
            await streamWriter.WriteLineAsync(streamResultPart);
            await streamWriter.FlushAsync();
        }

        // Log the final result
        Console.WriteLine("Meal plan created successfully!");
        await streamWriter.WriteLineAsync("Meal plan created successfully!");
        await streamWriter.FlushAsync();
    }

    }
}
