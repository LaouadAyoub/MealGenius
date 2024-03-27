using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Models.ModelsControllers;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using MealGeniusBackend.Services.RabbitMQ;

namespace MealGeniusBackend.Controllers
{
    /// <summary>
    /// AuthController
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;
        private readonly UserDbContext _dbcontext;
        private readonly RabbitMQService _rabbitMQService;

        public AuthController(UserManager<ApplicationUser> userManager, IAuthService authService,
                                ILogger<AuthController> logger,
                                IEmailService emailService, IUserService userService, UserDbContext dbcontext, SignInManager<ApplicationUser> signInManager, RabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
            _userManager = userManager;
            _authService = authService;
            _logger = logger;
            _emailService = emailService;
            _userService = userService;
            _dbcontext = dbcontext;
            _signInManager = signInManager;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login(UserLoginDto userLoginDto)
        {

            // Initialize the response object
            var loginOutput = new LoginOutput();

            ApplicationUser user = null;

            // Regular expression for validating an email address
            string emailRegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            bool isEmail = Regex.IsMatch(userLoginDto.Username, emailRegexPattern);

            if (isEmail)
            {
                user = await _userManager.FindByEmailAsync(userLoginDto.Username);
            }
            else
            {
                user = await _userManager.FindByNameAsync(userLoginDto.Username);
            }
            //if user is not found
            if (user == null)
            {
                loginOutput.Message = "User not found.";
                return NotFound(loginOutput);
            }

            var userInputs = _dbcontext.UserInputs.FirstOrDefault(UI => UI.User.Email == user.Email);
            var userData = userInputs.UserData;
            if (userData  == null)
            {
                loginOutput.Message = "User data not found, you should register again!";
                loginOutput.Email = user.Email;
                //delete the user 
                _dbcontext.Remove(user);
                return NotFound(loginOutput);
            }


            // Fill in the email for the response
            loginOutput.Email = user.Email;

            // Check if the user is confirmed
            loginOutput.IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

            // if email is not confirmed, we resend email confirmation
            //if (!loginOutput.IsEmailConfirmed)
            //{

            //    //await _emailService.SendEmailConfirmationAsync(user);
            //    loginOutput.Message = "Email not confirmed. A new confirmation email has been sent.";
            //    return BadRequest(loginOutput);
            //}

            // Check if the user has a password set
            loginOutput.IsPasswordSet = await _userManager.HasPasswordAsync(user);

            if (!loginOutput.IsPasswordSet)
            {
                loginOutput.Message = "User exists but no password is set. Please set up a password.";
                return BadRequest(loginOutput);
            }

            // Check if the username is set (assuming it's not set to the email by default)
            loginOutput.IsUsernameSet = !string.IsNullOrWhiteSpace(user.UserName) && user.UserName != user.Email;

            // Check password validity
            if (!await _userManager.CheckPasswordAsync(user, userLoginDto.Password))
            {
                loginOutput.Message = "Invalid login attempt.";
                return Unauthorized(loginOutput);
            }
            // User is successfully logged in
            loginOutput.IsLoginSuccess = true;
            loginOutput.Message = "Login successful";
            loginOutput.Email = user.Email;
            var token = await _authService.GenerateToken(user);
            SetAuthTokenCookie(token);

            return Ok(loginOutput);
        }

        //[HttpGet("GoogleLogin")]
        //public IActionResult GoogleLogin(string returnUrl = "/")
        //{
        //    var redirectUrl = Url.Action(nameof(GoogleResponse), "Auth", new { returnUrl });
        //    var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
        //    return Challenge(properties, "Google");
        //}

        //[HttpGet("signin-google")]
        //public async Task<IActionResult> GoogleResponse(string returnUrl = "http://localhost:3000/login-successful")
        //{
        //    var info = await _signInManager.GetExternalLoginInfoAsync();
        //    if (info == null)
        //    {
        //        // Redirect to the React app's login page with an error parameter
        //        return Redirect($"{returnUrl}?error=login-failed");
        //    }
        //    var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

        //    if (result.Succeeded)
        //    {
        //        return LocalRedirect(returnUrl);
        //    }   
        //    else
        //    {
        //        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        //            var users = await _userManager.Users.Where(u => u.Email == email).ToListAsync();
        //        var user = users.FirstOrDefault();
        //        if (user == null)
        //        {
        //            user = new IdentityUser { UserName = email, Email = email };
        //            await _userManager.CreateAsync(user);
        //            await _userManager.AddLoginAsync(user, info);
        //            await _signInManager.SignInAsync(user, isPersistent: false);
        //            return Redirect(returnUrl);
        //        }
        //        else
        //        {
        //            await _userManager.AddLoginAsync(user, info);
        //            await _signInManager.SignInAsync(user, isPersistent: false);
        //            return Redirect(returnUrl);
        //        }
        //    }
        //}

        public class LoginOutput
        {
            public bool IsLoginSuccess { get; set; }
            public string Message { get; set; }
            public bool IsEmailConfirmed { get; set; }
            public string Email { get; set; }
            public bool IsPasswordSet { get; set; }
            public bool IsUsernameSet { get; set; }
        }


        [HttpPost("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail([FromBody] EmailConfirmationModel model)
        {
            var output = new EmailConfirmationOutput();

            try
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null || user.Email.IsNullOrEmpty())
                {
                    output.Message = "User not found.";
                    return BadRequest(output);
                }


                // Check if the user has set a password
                output.isPasswordSet = await _userManager.HasPasswordAsync(user);

                // Check if the username is set
                output.isUsernameSet = !string.IsNullOrWhiteSpace(user.UserName) && user.UserName != user.Email;

                // if user exist and has password and username, we log in the user
                if (output.isPasswordSet && output.isUsernameSet)
                {
                    var token = await _authService.GenerateToken(user);
                    SetAuthTokenCookie(token);
                    output.Message = "User activated, Login successfull";
                    output.isConfirmed = user.EmailConfirmed;
                    output.SetConfirmedAt(user.EmailConfirmedAt ?? default(DateTime)); // or some other default value
                    output.Email = user.Email!;
                    output.isUsernameSet = true;
                    output.isPasswordSet = true;
                    return Ok(output);
                }
                if(output.isPasswordSet)
                {
                    output.Message = "User exist but no username is set. Please set up a username.";
                    output.isConfirmed = user.EmailConfirmed;
                    output.SetConfirmedAt(user.EmailConfirmedAt ?? default(DateTime)); // or some other default value
                    output.Email = user.Email;
                    output.isUsernameSet = false;
                    output.isPasswordSet = true;
                    return Ok(output);
                }
                if(output.isUsernameSet)
                {
                    output.Message = "User exist but no password is set. Please set up a password.";
                    output.isConfirmed = user.EmailConfirmed;
                    output.SetConfirmedAt(user.EmailConfirmedAt ?? default(DateTime)); // or some other default value
                    output.Email = user.Email;
                    output.isUsernameSet = true;
                    output.isPasswordSet = false;
                    return Ok(output);
                }
                // Check if the email is already verified
                if (user.EmailConfirmed)
                {
                    output.Email = user.Email;
                    output.isConfirmed = true;
                    output.SetConfirmedAt(user.EmailConfirmedAt ?? default(DateTime)); // or some other default value
                    output.Message = "Email is already confirmed.";
                    output.isExpired = false;
                    //return StatusCode(StatusCodes.Status409Conflict, output); // 409 for email already verified
                    return Ok(output);
                }

                if (await IsTokenExpired(model.UserId, model.Token))
                {
                    output.Email = user.Email;
                    output.Message = "Token expired.";
                    output.isConfirmed = false;
                    output.isExpired = true;
                    return StatusCode(StatusCodes.Status401Unauthorized, output); // 401 for token expired
                }




                var confirmResult = await _userManager.ConfirmEmailAsync(user, model.Token);
                if (!confirmResult.Succeeded)
                {
                    _logger.LogWarning("Email confirmation failed for user with ID: {UserId}", model.UserId);
                    output.Message = confirmResult.Errors.FirstOrDefault()?.Description ?? "Error confirming email.";
                    output.isConfirmed = false;
                    if(confirmResult.Errors.FirstOrDefault()?.Code == "InvalidToken")
                    {
                        output.Message = "Invalid token.";
                        return StatusCode(StatusCodes.Status401Unauthorized, output); // 401 for token expired
                    }
                    return BadRequest(output);
                }
                // execute user task if it's not already done :
                // Retrieve the latest UserTask and check if its status is new for the user
                var userTask = await _dbcontext.Tasks
                    .Where(ut => ut.UserId == user.Id)
                    .OrderByDescending(ut => ut.CreatedAt)
                    .FirstOrDefaultAsync();
                if(userTask.Status == UserTaskStatus.NotStarted)
                {
                    string userTaskMessage = JsonConvert.SerializeObject(userTask);
                    _rabbitMQService.PublishMessageInTaskQueue(userTaskMessage);
                    _logger.LogInformation("Email confirmed for user with ID: {UserId} and task message published to queue.", user.Id);
                }

                // Assuming your User entity has a property named EmailConfirmedAt
                user.EmailConfirmedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user); // Save the change to the database
                output.Message = "Email confirmed.";
                output.isConfirmed = true;
                output.SetConfirmedAt(DateTime.UtcNow); // You already have this
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

        private void SetAuthTokenCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Adjust based on environment
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(1),
            };

            Response.Cookies.Append("AuthToken", token, cookieOptions);
        }


        [HttpPost]
        [Route("SetupPassword")]
        public async Task<IActionResult> SetupPassword([FromBody] PasswordSetupModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            var setPasswordResult = await _userManager.AddPasswordAsync(user, model.Password);
            if (!setPasswordResult.Succeeded)
            {
                return BadRequest(new { ErrorMessage = setPasswordResult.Errors.FirstOrDefault()?.Description ?? "Failed to set password, please try again !"});
            }

            return Ok(new { Message = "Password setup successful" });
        }

        [HttpPost]
        [Route("SetupUsername")]
        public async Task<IActionResult> SetupUsername([FromBody] UsernameSetupModel model)
        {
            // Find the user by their ID
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // Check if the desired username is already taken by another user
            var existingUser = await _userManager.FindByNameAsync(model.Username);
            if (existingUser != null)
            {
                return BadRequest(new { ErrorMessage = "Username is already taken." });
            }

            // Set the new username for the user
            user.UserName = model.Username;
            var setUsernameResult = await _userManager.UpdateAsync(user);
            if (!setUsernameResult.Succeeded)
            {
                // Return any errors if the username could not be set
                return BadRequest(new { ErrorMessage = "Failed to set username.", Errors = setUsernameResult.Errors });
            }

            // Return a success message indicating the username has been set
            return Ok(new { Message = "Username setup successful" });
        }


        [HttpPost]
        [Route("SetupAccount")]
        public async Task<IActionResult> SetupAccount([FromBody] AccountSetupModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }
            var existingUser = await _userManager.FindByNameAsync(model.Username);
            if (existingUser != null)
            {
                return BadRequest(new { ErrorMessage = "Username is already taken." });
            }

            // Update the username
            user.UserName = model.Username;
            var setUsernameResult = await _userManager.UpdateAsync(user);
            if (!setUsernameResult.Succeeded)
            {
                return BadRequest(new { ErrorMessage = "Failed to set username.", Errors = setUsernameResult.Errors });
            }

            // Set the password
            var setPasswordResult = await _userManager.AddPasswordAsync(user, model.Password);
            if (!setPasswordResult.Succeeded)
            {
                return BadRequest(new { ErrorMessage = "Failed to set password.", Errors = setPasswordResult.Errors });
            }

            // After successfully updating the user's username and password
            var token = await _authService.GenerateToken(user);
            SetAuthTokenCookie(token);

            return Ok(new { Message = "Account setup and login successful" });
        }

        public class AccountSetupModel
        {
            public string UserId { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }
        public class PasswordSetupModel
        {
            public string UserId { get; set; }
            public string Password { get; set; }
        }
        public class UsernameSetupModel
        {
            public string UserId { get; set; }
            public string Username { get; set; }
        }



    }


}
