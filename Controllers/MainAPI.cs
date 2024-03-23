using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Models.ModelsControllers;
using MealGeniusBackend.Services.RabbitMQ;
using MealGeniusBackend.Services.Auth;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog; // add this line
using Stripe;
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
        //add json serializer


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
            var createResult = await _userService.CreateUserAsync(inputData.UserDetails.Email);

            if (!createResult.Result.Succeeded)
            {
                // Handle failed user creation
                return BadRequest(createResult.Result.Errors);
            }
            // Serialize inputData into a string
            string serializedInputData = JsonConvert.SerializeObject(inputData);

            // Create a new UserInput instance and add it to the database
            var userInput = new UserInput
            {
                UserData = serializedInputData,
                UserId = createResult.User.Id,
                Task = new UserTask
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    User = createResult.User
                }
            };


            _dbcontext.UserInputs.Add(userInput);
            await _dbcontext.SaveChangesAsync(); // Don't forget to save changes to the database

            // Send confirmation email (now includes token generation and link construction)
            await _emailService.SendConfirmationEmail(createResult.User, inputData.UserDetails.Name);


            // Continue with user registration process...
            return Ok("Registration successful! Please check your email to confirm your account.");
        }




        [HttpPost("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail([FromBody] EmailConfirmationModel model)
        {
            var output = new EmailConfirmationOutput();

            try
            {
                if (await IsTokenExpired(model.UserId, model.Token))
                {
                    output.Message = "Token expired.";
                    output.isConfirmed = false;
                    output.isExpired = true; // Indicate the token is expired
                    return BadRequest(output);
                }

                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                {
                    output.Message = "User not found.";
                    return BadRequest(output);
                }

                var confirmResult = await _userService.ConfirmEmailAsync(model.UserId, model.Token);
                if (!confirmResult)
                {
                    _logger.LogWarning("Email confirmation failed for user with ID: {UserId}", model.UserId);
                    output.Message = "Error confirming email.";
                    output.isConfirmed = false;
                    return BadRequest(output);
                }

                // Check if the user has set a password
                output.isPasswordSet = await _userManager.HasPasswordAsync(user);

                // Check if the username is set
                output.isUsernameSet = !string.IsNullOrWhiteSpace(user.UserName) && user.UserName != user.Email;

                // Update output with confirmation details
                output.Message = "Email confirmed.";
                output.isConfirmed = true;
                output.SetConfirmedAt(DateTime.UtcNow);
                output.Email = user.Email;

                return Ok(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while confirming email for user with ID: {UserId}", model.UserId);
                output.Message = "An internal error occurred.";
                return StatusCode(StatusCodes.Status500InternalServerError, output);
            }
        }


        private async Task<bool> IsTokenExpired(string userId, string token)
        {
            var tokenEntry = await _dbcontext.ConfirmationTokens
                .Where(t => t.UserId == userId && t.Token == token)
                .SingleOrDefaultAsync();

            if (tokenEntry == null)
            {
                return true; // Token not found, treat as expired or invalid
            }

            var expiryPeriod = TimeSpan.FromHours(24); // Example: 24 hours
            return DateTime.UtcNow - tokenEntry.IssuedAt > expiryPeriod;
        }


        // In MainAPIController
        [HttpGet("ConfirmEmailGet")]
        public async Task<IActionResult> ConfirmEmailGet(string userId, string token)
        {
            try
            {
                var confirmResult = await _userService.ConfirmEmailAsync(userId, token);
                if (!confirmResult)
                {
                    _logger.LogWarning("Email confirmation failed for user with ID: {UserId}", userId);
                    return BadRequest("Error confirming email.");
                }

                return Ok("Email confirmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while confirming email for user with ID: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }



        [HttpPost("delete-user-by-email")]
        public async Task<IActionResult> DeleteUserByEmail([FromBody] UserEmailModel userEmailModel)
        {
            var email = userEmailModel.Email;
            // Validate the input
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email is required.");
            }

            // Normalize the email if your database stores it in a normalized format
            var normalizedEmail = email.ToUpperInvariant();

            // Retrieve the user from the database by email
            var users = await _dbcontext.Users
                                        .Where(u => u.NormalizedEmail == normalizedEmail)
                                        .ToListAsync();
            
            var userByUserName = await _userManager.FindByNameAsync(email);

            if (users == null || users.Count == 0)
            {
                return NotFound($"Users with email {email}  not found.");
            }

            if (userByUserName != null)
            {
                _dbcontext.Users.Remove(userByUserName);
            }   

            // If the user is found, remove it from the context
            // Remove all users found from the context
            _dbcontext.Users.RemoveRange(users);
            
            await _dbcontext.SaveChangesAsync();

            return Ok($"User with email {email} has been deleted."); // Returns a 200 OK response
        }
        public class UserEmailModel
        {
            public string Email { get; set; }
        }



        //[Authorize]
        [HttpGet("ExecuteTask")]
        public async Task<IActionResult> ExecuteUserTask()
        {
            try
            {
                var username = "MoroccanCuisineLover";
                // Get the current authenticated user
                //var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
                var user = await _userManager.FindByNameAsync(username);
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
                if (userTask == null)
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
