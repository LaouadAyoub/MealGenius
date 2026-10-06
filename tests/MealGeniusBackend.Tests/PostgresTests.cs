using System.Security.Cryptography;
using System.Text;
using MealGeniusBackend.Controllers;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Npgsql;
using Xunit;

namespace MealGeniusBackend.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEALGENIUS_TEST_POSTGRES")))
            Skip = "Set MEALGENIUS_TEST_POSTGRES to an isolated test database; CI supplies PostgreSQL.";
    }
}

public sealed class PostgresTests
{
    [PostgresFact]
    public async Task Historical_migrations_replay_and_completed_delivery_is_idempotent()
    {
        await using var database = await TestDatabase.Create();
        using var provider = database.Services();
        Guid id;
        string userId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var user = new ApplicationUser { Email = "synthetic@example.test", UserName = "synthetic", PaymentConfirmed = true };
            var task = new UserTask { Id = Guid.NewGuid(), User = user };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            id = task.Id; userId = user.Id;
        }
        var message = new UserTaskDTO { Id = id, UserId = userId };
        for (var delivery = 0; delivery < 2; delivery++)
        {
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<GenerationJobProcessor>().Process(message, CancellationToken.None);
        }
        using var final = provider.CreateScope();
        var context = final.ServiceProvider.GetRequiredService<UserDbContext>();
        Assert.Equal(UserTaskStatus.Completed, (await context.Tasks.SingleAsync()).Status);
        Assert.Single(await context.MealPlans.ToListAsync());
        Assert.Single(await context.UserDashboards.ToListAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(4, provider.GetRequiredService<StageCounter>().Calls);
    }

    [PostgresFact]
    public async Task Partial_groceries_resume_enrichment_without_calling_AI()
    {
        await using var database = await TestDatabase.Create();
        await using var db = database.Context();
        var task = new UserTask { Id = Guid.NewGuid(), User = new ApplicationUser { Email = "test@example.test", UserName = "test" } };
        db.UserInputs.Add(new UserInput { Task = task, User = task.User, UserData = "{}" });
        db.MealPlans.Add(new MealPlan
        {
            Task = task, User = task.User, Title = "test", MealPlanJson = "{}",
            GroceryListJson = "{\"GroceryCategories\":[{\"CategoryName\":\"Fruit\",\"GroceryItems\":[{\"GroceryItemName\":\"Apples\",\"SimilarNames\":null}]}]}"
        });
        db.GroceryItems.Add(new MealGeniusBackend.DataAcess.GroceryItem
        {
            GroceryItemId = Guid.NewGuid(), Name = "Apple", SimilarNames = ["Apples"], Category = "Fruit",
            ImageUrl = "https://example.test/apple.jpg", CompressedImageUrl = ""
        });
        await db.SaveChangesAsync();
        // No AI service: reaching the generation branch would fail this test.
        var service = new GroceryListService(db, null!, NullLogger<GroceryListService>.Instance);
        await service.GenerateGroceryList(new UserTaskDTO { Id = task.Id, UserId = task.UserId });
        var plan = await db.MealPlans.SingleAsync();
        Assert.Equal(1, plan.GroceryListVersion);
        Assert.Contains("https://example.test/apple.jpg", plan.GroceryListJson);
    }

    [PostgresFact]
    public async Task Signed_Stripe_replay_only_dispatches_and_emails_once()
    {
        await using var database = await TestDatabase.Create();
        await using var db = database.Context();
        var user = new ApplicationUser { Email = "checkout@example.test", UserName = "checkout" };
        db.Users.Add(user); await db.SaveChangesAsync();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EndpointSecret"] = secret, ["Stripe:ExpectedAmountTotal"] = "500", ["Stripe:Currency"] = "usd"
        }).Build();
        var json = JsonConvert.SerializeObject(new
        {
            id = "evt_synthetic", @object = "event", type = "checkout.session.completed", api_version = "2023-10-16",
            data = new { @object = new { id = "cs_synthetic", @object = "checkout.session", payment_status = "paid",
                amount_total = 500, currency = "usd", client_reference_id = user.Id, customer_details = new { email = user.Email } } }
        });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(timestamp + "." + json))).ToLowerInvariant();
        var effects = new PaymentEffects();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var controller = new StripeWebhookController(configuration, db, effects, effects, NullLogger<StripeWebhookController>.Instance);
            var http = new DefaultHttpContext();
            http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            http.Request.Headers["Stripe-Signature"] = $"t={timestamp},v1={signature}";
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            Assert.IsType<OkResult>(await controller.Handle());
        }
        Assert.Equal(1, effects.Jobs); Assert.Equal(1, effects.Emails);
        Assert.True(user.PaymentConfirmed);
        Assert.Single(await db.ProcessedStripeEvents.ToListAsync());
    }

    private sealed class PaymentEffects : IExecuteTaskService, IEmailService
    {
        public int Jobs; public int Emails;
        public Task<bool> ExecuteUserTask(string email) { Jobs++; return Task.FromResult(true); }
        public Task SendPaymentConfirmationEmail(string email) { Emails++; return Task.CompletedTask; }
        public Task SendConfirmationEmail(ApplicationUser user, string name) => Task.CompletedTask;
        public Task SendPasswordResetEmail(ApplicationUser user) => Task.CompletedTask;
    }
}

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string schema = "mgtest_" + Guid.NewGuid().ToString("N");
    private string connection = "";
    public static async Task<TestDatabase> Create()
    {
        var result = new TestDatabase();
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MEALGENIUS_TEST_POSTGRES"));
        if (builder.Database?.Contains("test", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("Integration tests require a database name containing 'test'.");
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA {result.schema}", connection);
        await command.ExecuteNonQueryAsync();
        builder.SearchPath = result.schema;
        result.connection = builder.ConnectionString;
        await using var db = result.Context();
        await db.Database.MigrateAsync();
        return result;
    }
    public UserDbContext Context() => new(new DbContextOptionsBuilder<UserDbContext>().UseNpgsql(connection).Options);
    public ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<UserDbContext>(o => o.UseNpgsql(connection));
        services.AddSingleton<GenerationCancellation>();
        services.AddSingleton<StageCounter>();
        services.AddScoped<IUserDashboardService, FakeStages>();
        services.AddScoped<IMealPlanService, FakeStages>();
        services.AddScoped<IGroceryListService, FakeStages>();
        services.AddScoped<IMealsImagesService, FakeStages>();
        services.AddScoped<GenerationJobProcessor>();
        return services.BuildServiceProvider();
    }
    public async ValueTask DisposeAsync()
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", db);
        await command.ExecuteNonQueryAsync();
    }
}
internal sealed class StageCounter { public int Calls; }
internal sealed class FakeStages(UserDbContext db, StageCounter counter) :
    IUserDashboardService, IMealPlanService, IGroceryListService, IMealsImagesService
{
    public async Task GenerateUserDashboard(UserTaskDTO task)
    {
        Interlocked.Increment(ref counter.Calls);
        db.UserDashboards.Add(new UserDashboard { Id = Guid.NewGuid(), UserId = task.UserId, TaskId = task.Id,
            MacroTargets = "a", MicroGuide = "b", WaterIntake = "c", UserGoalsGuide = "d", JsonUserKeyInfos = "{}" });
        await db.SaveChangesAsync();
    }
    public async Task GenerateMealPlan(UserTaskDTO task)
    {
        Interlocked.Increment(ref counter.Calls);
        db.MealPlans.Add(new MealPlan { Id = Guid.NewGuid(), UserId = task.UserId, TaskId = task.Id, Title = "test",
            MealPlanJson = JsonConvert.SerializeObject(new UserMealsRoot { UserMeals = [new aMeal { MealImage = "https://example.test/image.jpg" }] }),
            GroceryListJson = "" });
        await db.SaveChangesAsync();
    }
    public async Task GenerateGroceryList(UserTaskDTO task)
    {
        Interlocked.Increment(ref counter.Calls);
        var plan = await db.MealPlans.SingleAsync(p => p.TaskId == task.Id);
        plan.GroceryListJson = "{}"; plan.GroceryListVersion = 1;
        await db.SaveChangesAsync();
    }
    public async Task GenerateMealsImages(UserTaskDTO task)
    {
        Interlocked.Increment(ref counter.Calls);
        var entity = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        entity.MealsImagesStatus = UserMealsImagesStatus.Completed;
        await db.SaveChangesAsync();
    }
}
