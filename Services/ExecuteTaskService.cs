using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services.RabbitMQ;
using Microsoft.EntityFrameworkCore;

namespace MealGeniusBackend.Services;
public interface IExecuteTaskService { Task<bool> ExecuteUserTask(string email); }

public class ExecuteTaskService(UserDbContext db, RabbitMQService broker) : IExecuteTaskService
{
    public async Task<bool> ExecuteUserTask(string email)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        if (user is null || !user.PaymentConfirmed) return false;
        var task = await db.Tasks.Where(t => t.UserId == user.Id).OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
        if (task is null || task.Status is UserTaskStatus.Completed or UserTaskStatus.Ongoing) return false;
        if (!await db.UserInputs.AnyAsync(i => i.TaskId == task.Id)) return false;
        broker.Publish(new UserTaskDTO { Id = task.Id, UserId = user.Id });
        return true;
    }
}
