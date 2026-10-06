using System.Net;
using System.Text;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Helpers;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace MealGeniusBackend.Tests;

public class GenerationTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Permanent_AI_errors_terminate_after_one_request(HttpStatusCode status)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(status));
        var service = CreateAI(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GenerateImageForMealAsync("test meal"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Transient_AI_errors_stop_at_the_configured_limit()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<HttpRequestException>(() => CreateAI(handler).GenerateImageForMealAsync("test meal"));
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("stop", "not JSON")]
    [InlineData("length", "{}")]
    public async Task Malformed_or_truncated_AI_output_is_not_persisted_or_silently_retried(string reason, string content)
    {
        var body = JsonConvert.SerializeObject(new { choices = new[] { new { finish_reason = reason, message = new { content } } } });
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateAI(handler).GenerateJsonBasedOnPromptResponseAsync("system", "user", 1000));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Grocery_alias_matching_returns_the_actual_match_and_merges_without_duplicates()
    {
        var item = new MealGeniusBackend.DataAcess.GroceryItem
        {
            Name = "Apple", SimilarNames = ["Apples"], ImageUrl = "https://example.test/apple.jpg", CompressedImageUrl = ""
        };
        var found = GroceryImageMatcher.Find([item], "APPLES", null);
        Assert.Same(item, found);
        Assert.Equal(2, GroceryImageMatcher.MergeAliases(item.SimilarNames, ["apples", "Fresh apple"]).Count);
    }

    [Fact]
    public void Job_completion_requires_every_stage_including_images()
    {
        var dashboard = new UserDashboard
        {
            MacroTargets = "a", MicroGuide = "b", WaterIntake = "c", UserGoalsGuide = "d", JsonUserKeyInfos = "{}"
        };
        var plan = new MealPlan
        {
            GroceryListVersion = 1, GroceryListJson = "{}",
            MealPlanJson = JsonConvert.SerializeObject(new UserMealsRoot { UserMeals = [new aMeal { MealImage = "https://example.test/meal.jpg" }] })
        };
        var task = new UserTask { MealsImagesStatus = UserMealsImagesStatus.Ongoing };
        Assert.Throws<InvalidDataException>(() => GenerationJobProcessor.EnsureComplete(dashboard, plan, task));
        task.MealsImagesStatus = UserMealsImagesStatus.Completed;
        GenerationJobProcessor.EnsureComplete(dashboard, plan, task);
    }

    [Fact]
    public void Email_is_not_sent_to_the_generation_models()
    {
        Assert.DoesNotContain("Email", PromptPrivacy.RemoveEmail("{\"Details\":{\"Email\":\"synthetic@example.test\",\"Age\":\"30\"}}"));
    }

    private static OpenAIService CreateAI(FakeHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OPENAI_API_KEY"] = "synthetic-test-value", ["OpenAI:MaxAttempts"] = "2"
        }).Build();
        return new OpenAIService(new HttpClient(handler), config, new OpenAIConcurrency(config),
            new GenerationCancellation(), NullLogger<OpenAIService>.Instance);
    }
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}
