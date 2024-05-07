using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services.RabbitMQ;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Services
{

    public interface IExecuteTaskService
    {
        Task<bool> ExecuteUserTask(string email);

    }
    public class ExecuteTaskService : IExecuteTaskService
    {
        private readonly ILogger<ExecuteTaskService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserDbContext _dbcontext;
        private readonly RabbitMQService _rabbitMQService;




        public ExecuteTaskService(IFluentEmailFactory emailFactory, ILogger<ExecuteTaskService> logger, UserManager<ApplicationUser> userManager, UserDbContext dbcontext, RabbitMQService rabbitMQService)
        {
            _logger = logger;
            _userManager = userManager;
            _dbcontext = dbcontext;
            _rabbitMQService = rabbitMQService;
        }

        public async Task<bool> ExecuteUserTask(string email)
        {
            try
            {
                //check if the user exists in the db
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                    return false;
                //.FirstOrDefaultAsync();
                var userTask = await _dbcontext.Tasks
                                         .Where(t => t.UserId == user.Id)
                                         .OrderByDescending(t => t.CreatedAt)
                                         .FirstOrDefaultAsync();

                if (userTask == null)
                    return false;

                if (userTask.Status != UserTaskStatus.Completed)
                {
                    string userTaskMessage = JsonConvert.SerializeObject(userTask, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });
                    _rabbitMQService.PublishMessageInTaskQueue(userTaskMessage);
                    _logger.LogInformation("Email confirmed for user with ID: {UserId} and task message published to queue.", user.Id);
                    return true;
                }
                return false;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Email template file not found.");
                throw new InvalidOperationException("The email confirmation process failed due to a missing template.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email.");
                throw new InvalidOperationException("An unexpected error occurred while sending the confirmation email.", ex);
            }
        }

    }
}
