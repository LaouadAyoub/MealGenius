using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services.Dashboard;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Services;

public class GenerationJobProcessor(UserDbContext db, IServiceScopeFactory scopes,
    IUserDashboardService dashboard, IMealPlanService meals, IConfiguration configuration,
    GenerationCancellation cancellation, ILogger<GenerationJobProcessor> logger)
{
    public async Task Process(UserTaskDTO message, CancellationToken stoppingToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(configuration.GetValue("Generation:TimeoutMinutes", 20)));
        cancellation.Token = deadline.Token;
        await using var workLock = await DatabaseWorkLock.Acquire(db.Database.GetConnectionString()!,
            "generation:" + message.Id, deadline.Token);
        var task = await db.Tasks.Include(t => t.User).SingleOrDefaultAsync(t => t.Id == message.Id, deadline.Token)
            ?? throw new InvalidDataException("Task does not exist.");
        if (task.UserId != message.UserId || !task.User.PaymentConfirmed)
            throw new InvalidDataException("Task owner or entitlement is invalid.");
        if (task.Status == UserTaskStatus.Completed) return;
        try
        {
            task.Status = UserTaskStatus.Ongoing;
            await db.SaveChangesAsync(deadline.Token);
            // Services resume from persisted stage output; automatic retries are confined to transient HTTP requests.
            await dashboard.GenerateUserDashboard(message);
            await meals.GenerateMealPlan(message);
            await Task.WhenAll(
                WithScope(s => s.GetRequiredService<IGroceryListService>().GenerateGroceryList(message)),
                WithScope(s => s.GetRequiredService<IMealsImagesService>().GenerateMealsImages(message)));
            db.ChangeTracker.Clear();
            task = await db.Tasks.SingleAsync(t => t.Id == message.Id, deadline.Token);
            var plan = await db.MealPlans.SingleAsync(m => m.TaskId == message.Id, deadline.Token);
            var board = await db.UserDashboards.SingleAsync(d => d.TaskId == message.Id, deadline.Token);
            EnsureComplete(board, plan, task);
            task.Status = UserTaskStatus.Completed;
            await db.SaveChangesAsync(deadline.Token);
            logger.LogInformation("Generation task {TaskId} completed.", task.Id);
        }
        catch
        {
            db.ChangeTracker.Clear();
            await db.Tasks.Where(t => t.Id == message.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Status, UserTaskStatus.Failed), CancellationToken.None);
            throw;
        }
        finally { cancellation.Token = default; }
    }
    private async Task WithScope(Func<IServiceProvider, Task> action)
    {
        using var scope = scopes.CreateScope();
        await action(scope.ServiceProvider);
    }
    public static void EnsureComplete(UserDashboard dashboard, MealPlan plan, UserTask task)
    {
        if (new[] { dashboard.MacroTargets, dashboard.MicroGuide, dashboard.WaterIntake,
            dashboard.UserGoalsGuide, dashboard.JsonUserKeyInfos }.Any(string.IsNullOrWhiteSpace)
            || plan.GroceryListVersion < 1 || string.IsNullOrWhiteSpace(plan.GroceryListJson)
            || task.MealsImagesStatus != UserMealsImagesStatus.Completed)
            throw new InvalidDataException("Generation stages are incomplete.");
        var meals = JsonConvert.DeserializeObject<UserMealsRoot>(plan.MealPlanJson)?.UserMeals;
        if (meals is null || meals.Count == 0 || meals.Any(m => string.IsNullOrWhiteSpace(m.MealImage)))
            throw new InvalidDataException("Generated meals or images are missing.");
    }
}
