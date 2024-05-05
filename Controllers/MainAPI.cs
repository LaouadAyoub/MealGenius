using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Mapper;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using MealGeniusBackend.Services.RabbitMQ;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using MealGeniusBackend.Middleware;
using Microsoft.IdentityModel.Tokens;
using MealGeniusBackend.Models.AuthControllerRecords;
using MealGeniusBackend.Models.Enums;

namespace MealGeniusBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MainAPIController : ControllerBase
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly UserDbContext _dbcontext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly ILogger<MainAPIController> _logger;
        //add json serializer


        public MainAPIController(RabbitMQService rabbitMQService, UserDbContext dbcontext
            , UserManager<ApplicationUser> userManager, IEmailService emailService, IUserService userService, ILogger<MainAPIController> logger)
        {
            _rabbitMQService = rabbitMQService;
            _userManager = userManager;
            _dbcontext = dbcontext;
            _emailService = emailService;
            _userService = userService;
            _logger = logger;
        }
        [HttpPost("RegisterUser")]
        public async Task<IActionResult> RegisterUser([FromBody] UserProfile inputData)
        {
            try
            {
                var registerOut = new RegisterOut();
                var UserData = DtoMapper.MapUserProfileToUserData(inputData);
                string serializedInputData = JsonConvert.SerializeObject(UserData);

                // Check if the user already exists
                var existingUser = await _userManager.FindByEmailAsync(inputData.Email);
                if (existingUser != null)
                {
                    // Check existing user inputs
                    var existingInputs = await _dbcontext.UserInputs.FirstOrDefaultAsync(input => input.UserId == existingUser.Id);
                    if (existingInputs != null)
                    {
                        var userHasDashboards = await _userService.UserHasDashboards(existingUser);
                        if (userHasDashboards)
                        {
                            return Ok(new RegisterOut
                            {
                                Message = "User already exists please try a new email adress or login",
                                Status = UserStatus.Active,
                                Email = inputData.Email
                            });
                        }

                        return await HandleExistingUser(existingUser, existingInputs, serializedInputData, inputData);
                    }

                    // If no existing inputs, add new inputs
                    var userInput = new UserInput
                    {
                        UserData = serializedInputData,
                        UserId = existingUser.Id,
                        Task = new UserTask
                        {
                            Id = Guid.NewGuid(),
                            CreatedAt = DateTime.UtcNow,
                            User = existingUser
                        }
                    };
                    _dbcontext.UserInputs.Add(userInput);
                    await _dbcontext.SaveChangesAsync();

                    await SendConfirmationEmailAndUpdateTimestamp(existingUser, inputData.Name);
                    registerOut.Message = "User exists already, your data has been updated, and the email confirmation has been resent. Please check your email.";
                    registerOut.Status = UserStatus.AccountNotConfirmed;
                    return Ok(registerOut);
                }

                // Create new user
                return await CreateNewUser(inputData, serializedInputData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the registration process.");
                return StatusCode(500, "An internal error occurred. Please try again later.");
            }
        }
        private async Task<IActionResult> HandleExistingUser(ApplicationUser existingUser, UserInput existingInputs, string serializedInputData, UserProfile inputData)
        {
            // Check for recent email confirmation attempt
            if (existingUser.ConfirmationEmailSentAt.HasValue &&
                DateTime.UtcNow - existingUser.ConfirmationEmailSentAt.Value < TimeSpan.FromMinutes(2))
            {
                var timeLeft = TimeSpan.FromMinutes(2) - (DateTime.UtcNow - existingUser.ConfirmationEmailSentAt.Value);
                var roundedSeconds = 5 * Math.Ceiling(timeLeft.Seconds / 5.0); // Round up to the nearest multiple of 5

                var timeComponent = timeLeft.TotalMinutes >= 1 ?
                    $"{timeLeft.Minutes} minutes and {roundedSeconds} seconds" :
                    $"{roundedSeconds} seconds";

                var message = $"Email confirmation has been sent. Please check your inbox and other email folders, or you can retry to resend the email in {timeComponent}.";
                return Ok(new RegisterOut
                {
                    Message = message,
                    Status = UserStatus.AccountNotConfirmed,
                    Email = inputData.Email
                });
            }

            // Check if user data needs updating
            if (existingInputs.UserData != serializedInputData)
            {
                existingInputs.UserData = serializedInputData;
                _dbcontext.Update(existingInputs);
                await _dbcontext.SaveChangesAsync();
            }

            // Resend confirmation email if necessary
            await ResendConfirmationEmail(existingUser, inputData.Name);
            return Ok(new RegisterOut
            {
                Message = "User exists already, your data has been updated, and the email confirmation has been resent. Please check your email.",
                Status = UserStatus.InputsbutNoDashboards
            });
        }
        private async Task<IActionResult> CreateNewUser(UserProfile inputData, string serializedInputData)
        {
            var createResult = await _userService.CreateUserAsync(inputData.Email, inputData.Name);
            if (!createResult.Result.Succeeded)
            {
                return BadRequest(createResult.Result.Errors);
            }

            // Create user input data and associate with new user
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
            await _dbcontext.SaveChangesAsync();

            // Send email confirmation
            await SendConfirmationEmailAndUpdateTimestamp(createResult.User, inputData.Name);
            return Ok(new RegisterOut
            {
                Message = "Registration successful! Please check your email to confirm your account.",
                Status = UserStatus.AccountNotConfirmed
            });
        }


        [HttpPost("RegisterUserBackend")]
        public async Task<IActionResult> RegisterUserBackend([FromBody] UserData inputData)
        {
            try
            {

                var existingUser = await _userManager.FindByEmailAsync(inputData.Details.Email);
                string serializedInputData = JsonConvert.SerializeObject(inputData);
                var userInput = new UserInput();

                if (existingUser != null)
                {
                    var existingInputs = await _dbcontext.UserInputs.FirstOrDefaultAsync(input => input.UserId == existingUser.Id);
                    if (existingInputs is not null && !existingInputs.UserData.IsNullOrEmpty())
                    {

                        var existingUserTask = await _dbcontext.Tasks.FirstOrDefaultAsync(t => t.UserId == existingUser.Id);

                        if (existingUserTask != null && existingUserTask.Status == UserTaskStatus.Completed)
                        {
                            return BadRequest("User already exists, please login");
                        }

                        if (existingUser.ConfirmationEmailSentAt.HasValue &&
                            DateTime.UtcNow - existingUser.ConfirmationEmailSentAt.Value < TimeSpan.FromMinutes(3))
                        {
                            var timeLeft = TimeSpan.FromMinutes(2) - (DateTime.UtcNow - existingUser.ConfirmationEmailSentAt.Value);
                            var roundedSeconds = 5 * Math.Ceiling(timeLeft.Seconds / 5.0); // Round up to the nearest multiple of 5

                            var timeComponent = timeLeft.TotalMinutes >= 1 ?
                                $"{timeLeft.Minutes} minutes and {roundedSeconds} seconds" :
                                $"{roundedSeconds} seconds";

                            var message = $"Email confirmation has been sent. Please check your inbox and other email folders, or you can retry to resend the email in {timeComponent}.";

                            return BadRequest(new { message });
                        }
                        var userHasDashboards = await _userService.UserHasDashboards(existingUser);
                        if (!userHasDashboards)
                        {
                                existingInputs.UserData = serializedInputData;
                                _dbcontext.Update(existingInputs);
                                _dbcontext.SaveChanges();
                        }

                        await ResendConfirmationEmail(existingUser, inputData.Details.Email);
                        return Ok(new { message = "Email confirmation has been resent. Please check your email." });
                    }
                    userInput.UserData = serializedInputData;
                    userInput.UserId = existingUser.Id;
                    userInput.Task = new UserTask
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow,
                        User = existingUser
                    };
                    _dbcontext.UserInputs.Add(userInput);
                    await _dbcontext.SaveChangesAsync();
                    await SendConfirmationEmailAndUpdateTimestamp(existingUser, inputData.Details.Name);
                    return Ok(new { message = "Email confirmation has been resent. Please check your email." });
                }

                var createResult = await _userService.CreateUserAsync(inputData.Details.Email, inputData.Details.Name);
                if (!createResult.Result.Succeeded)
                {
                    return BadRequest(createResult.Result.Errors);
                }

                userInput.UserData = serializedInputData;
                userInput.UserId = createResult.User.Id;
                userInput.Task = new UserTask
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    User = createResult.User
                };
                _dbcontext.UserInputs.Add(userInput);
                await _dbcontext.SaveChangesAsync();

                await SendConfirmationEmailAndUpdateTimestamp(createResult.User, inputData.Details.Name);

                return Ok("Registration successful! Please check your email to confirm your account.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the registration process.");
                return StatusCode(500, "An internal error occurred. Please try again later.");
            }
        }


        private async Task SendConfirmationEmailAndUpdateTimestamp(ApplicationUser user, string name)
        {
            try
            {
                await _emailService.SendConfirmationEmail(user, name);
                user.ConfirmationEmailSentAt = DateTime.UtcNow;
                await _dbcontext.SaveChangesAsync();
                _logger.LogInformation("Confirmation email sent successfully and timestamp updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email or update timestamp.");
                throw; // Rethrow to handle the error further up the call stack.
            }
        }

        private async Task ResendConfirmationEmail(ApplicationUser user, string name)
        {
            await SendConfirmationEmailAndUpdateTimestamp(user, name);
            _logger.LogInformation($"Confirmation email resent to {user.Email}.");
        }



        [HttpPost]
        [Route("registerPayment")]
        [ApiKeyAuth]
        public IActionResult RegisterPayment([FromBody] PaymentRegistration paymentRegistration)
        {
            // Implement your logic here, e.g., saving the payment information to the database

            // For example:
            _logger.LogInformation($"Received payment from {paymentRegistration.Email} of amount {paymentRegistration.PaymentAmount}");

            var aPaymentRegistration = new PaymentRegistrationTable 
            {
                Email = paymentRegistration.Email,
                PaymentID = paymentRegistration.PaymentID,
                PaymentAmount = paymentRegistration.PaymentAmount,
                PaymentCurrency = paymentRegistration.PaymentCurrency,
                PaymentDate = paymentRegistration.PaymentDate,
                Country = paymentRegistration.Country
            };
            _dbcontext.PaymentRegistrations.Add(aPaymentRegistration);
            _dbcontext.SaveChanges();
            // Return a success response
            return Ok();
        }


        #region ConfirmEmailGet
        // In MainAPIController
        //[HttpGet("ConfirmEmailGet")]
        //public async Task<IActionResult> ConfirmEmailGet(string userId, string token)
        //{
        //    try
        //    {
        //        var confirmResult = await _userService.ConfirmEmailAsync(userId, token);
        //        if (!confirmResult)
        //        {
        //            _logger.LogWarning("Email confirmation failed for user with ID: {UserId}", userId);
        //            return BadRequest("Error confirming email.");
        //        }

        //        return Ok("Email confirmed");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "An error occurred while confirming email for user with ID: {UserId}", userId);
        //        return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
        //    }
        //}
        #endregion


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



        [Authorize]
        [HttpGet("ExecuteTask")]
        public async Task<IActionResult> ExecuteUserTask()
        {
            try
            {
                //var username = "MoroccanCuisineLover";
                // Get the current authenticated user
                var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
                //var user = await _userManager.FindByNameAsync(username);
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
                        Status = UserTaskStatus.NotStarted,
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

        [Authorize]
        [HttpGet("ReExecuteTask")]
        public async Task<IActionResult> ReExecuteUserTask([FromBody] UserData userData)
        {
            try
            {
                //var username = "MoroccanCuisineLover";
                // Get the current authenticated user
                var user = await _userManager.FindByNameAsync(User?.Identity?.Name);
                //var user = await _userManager.FindByNameAsync(username);
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
                        Status = UserTaskStatus.NotStarted,
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
                    Status = UserTaskStatus.TobeRetried
                };

                var userInputs = await _dbcontext.UserInputs.FirstOrDefaultAsync(u => u.UserId == user.Id);
                //update userInputs
                if (userInputs == null)
                    return NotFound();

                userInputs.UserData = JsonConvert.SerializeObject(userData);
                userInputs.Task = userTask;
                _dbcontext.UserInputs.Update(userInputs);
                await _dbcontext.SaveChangesAsync();



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
