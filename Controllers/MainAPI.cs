using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog; // add this line
using static MealGeniusBackend.Controllers.MainAPIController;

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPIController : ControllerBase
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly UserDbContext _dbcontext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly ILogger<MainAPIController> _logger;


        public MainAPIController(RabbitMQService rabbitMQService, UserDbContext dbcontext
            , UserManager<IdentityUser> userManager, IEmailService emailService, IUserService userService, ILogger<MainAPIController> logger)
        {
            _rabbitMQService = rabbitMQService;
            _userManager = userManager;
            _dbcontext = dbcontext;
            _emailService = emailService;
            _userService = userService;
            _logger = logger;
        }


        [HttpPost("RegisterUser")]
        public async Task<IActionResult> RegisterUser([FromBody] UserInputsDataModel inputData)
        {
            // Attempt to create the user
            var createResult = await _userService.CreateUserAsync(inputData.UserDetails.Email, inputData.UserDetails.UserName, inputData.UserDetails.Password);

            if (!createResult.Result.Succeeded)
            {
                // we return the errors from the IdentityResult and let react handle the errors
                return BadRequest(createResult.Result.Errors);
            }

            // User created, send confirmation email
            //var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(createResult.User);
            //var confirmationLink = Url.Action(nameof(ConfirmEmail), "MainAPI",
            //                            new { userId = createResult.User.Id, token = confirmationToken },
            //                            Request.Scheme);
            //await _emailService.SendConfirmationEmail(createResult.User.Email, confirmationLink);


            // Create and save UserTask
            var userTask = new UserTask
            {
                UserId = createResult.User.Id,
                Status = UserTaskStatus.New
            };
            await _dbcontext.AddAsync(userTask);

            // Create and save UserInput
            string serializedInputData = JsonConvert.SerializeObject(inputData);
            var userInput = new UserInput
            {
                UserId = createResult.User.Id,
                TaskId = userTask.Id,
                UserData = serializedInputData
            };
            await _dbcontext.AddAsync(userInput);
            await _dbcontext.SaveChangesAsync();

            return Ok("Registration successful! Please check your email to confirm your account.");
        }


        // In MainAPIController
        [HttpGet("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            try
            {
                var confirmResult = await _userService.ConfirmEmailAsync(userId, token);
                if (!confirmResult)
                {
                    _logger.LogWarning("Email confirmation failed for user with ID: {UserId}", userId);
                    return BadRequest("Error confirming email.");
                }

                var userTask = await _dbcontext.Tasks
                    .Where(ut => ut.UserId == userId)
                    .OrderByDescending(ut => ut.CreatedAt)
                    .FirstOrDefaultAsync();

                if (userTask == null || userTask.Status != UserTaskStatus.New)
                {
                    _logger.LogInformation("No new UserTask found for user with ID: {UserId}", userId);
                    return Ok("Email confirmed, but no new task is available.");
                }

                string userTaskMessage = JsonConvert.SerializeObject(userTask);
                _rabbitMQService.PublishMessageInTaskQueue(userTaskMessage);
                _logger.LogInformation("Email confirmed for user with ID: {UserId} and task message published to queue.", userId);

                return Ok("Email confirmed and meal plan is being prepared!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while confirming email for user with ID: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }


        [Authorize]
        [HttpGet("ExecuteTask")]
        public async Task<IActionResult> ExecuteUserTask()
        {
            try
            {
                // Get the current authenticated user
                var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
                if (user == null)
                {
                    _logger.LogWarning("ExecuteUserTask: User not found or not authenticated.");
                    return Unauthorized();
                }

                // Retrieve the latest UserTask and check if its status is new for the user
                var userTask = await _dbcontext.Tasks
                    .Where(ut => ut.UserId == user.Id)
                    .OrderByDescending(ut => ut.CreatedAt)
                    .FirstOrDefaultAsync();

                // If the task does not exist, create a new one
                if (userTask == null || userTask.Status != UserTaskStatus.New)
                {
                    userTask = new UserTask
                    {
                        UserId = user.Id,
                        Status = UserTaskStatus.New,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _dbcontext.Tasks.AddAsync(userTask);
                    await _dbcontext.SaveChangesAsync();
                    _logger.LogInformation("ExecuteUserTask: Created new task for user with ID: {UserId}", user.Id);
                }
                // Serialize the task and publish the message
                var userTaskDTO = new UserTaskDTO
                {
                    Id = userTask.Id,
                    UserId = userTask.UserId,
                    Status = userTask.Status
                };
                string userTaskMessage = JsonConvert.SerializeObject(userTaskDTO);
                _rabbitMQService.PublishMessageInTaskQueue(userTaskMessage);
                _logger.LogInformation("ExecuteUserTask: Task with ID: {TaskId} has been executed for user with ID: {UserId}.", userTask.Id, user.Id);

                return Ok($"The task with ID {userTask.Id} has been executed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteUserTask: An exception occurred");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while executing the task.");
            }
        }
    }
}
