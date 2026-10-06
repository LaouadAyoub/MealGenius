using System.Security.Claims;
using MealGeniusBackend.DataAcess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Controllers;

[Authorize(Policy = "PaidUser"), Route("api/[controller]"), ApiController]
public class StatusController(UserDbContext db) : ControllerBase
{
    [HttpGet("GetUserTask")]
    public async Task<IActionResult> GetUserTask()
    {
        var task = await Latest();
        if (task is null) return NotFound();
        var input = await db.UserInputs.AsNoTracking().SingleOrDefaultAsync(i => i.TaskId == task.Id);
        return Ok(new
        {
            userTaskid = task.Id, Status = task.Status.ToString(),
            UserOutputStatus = task.UserOutputStatus.ToString(), Imagestatus = task.MealsImagesStatus.ToString(),
            userData = input is null ? null : JsonConvert.DeserializeObject<object>(input.UserData)
        });
    }

    [HttpGet("GetStatus")]
    public async Task<IActionResult> GetStatus()
    {
        var task = await Latest();
        if (task is null) return NotFound();
        var plan = await db.MealPlans.AsNoTracking().SingleOrDefaultAsync(p => p.TaskId == task.Id);
        var dashboard = await db.UserDashboards.AsNoTracking().SingleOrDefaultAsync(d => d.TaskId == task.Id);
        return Ok(new
        {
            TaskId = task.Id, Status = task.Status.ToString(), UserOutputStatus = task.UserOutputStatus.ToString(),
            ImagesStatus = task.MealsImagesStatus.ToString(),
            DashboardVersion = dashboard?.UserDashboardVersion ?? 0,
            MealPlanVersion = plan?.MealPlanVersion ?? 0,
            GroceryListVersion = plan?.GroceryListVersion ?? 0,
            MealsImagesVersion = plan?.MealsImagesVersion ?? 0
        });
    }

    private Task<UserTask?> Latest() => db.Tasks.AsNoTracking()
        .Where(t => t.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
        .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
}
