using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Models.AuthControllerRecords;
using MealGeniusBackend.Models.Enums;
using MealGeniusBackend.Models.ModelsControllers;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using MealGeniusBackend.Services.RabbitMQ;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.RegularExpressions;

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

        [HttpPost("LoginEmail")]
        public async Task<IActionResult> LoginEmail(LoginEmailDto loginEmailDto)
        {
            var loginEmailOutput = new LoginEmailOut();

            var user = await _userManager.FindByEmailAsync(loginEmailDto.EmailOrUsername);
            if (user == null)
            {
                loginEmailOutput.Message = "Email not found.";
                loginEmailOutput.Status = UserStatus.UserNotFound;
                return NotFound(loginEmailOutput);
            }

            // Check if the user has a password set
            //var isPasswordSet = await _userManager.HasPasswordAsync(user);
            //var isConfirmed = user.EmailConfirmed;
            //if (!isPasswordSet)
            //{
            //    if (isConfirmed)
            //    {
            //        loginEmailOutput.Message = "Account confirmed but password not set";
            //        loginEmailOutput.Status = UserStatus.PasswordNotSet;
            //        loginEmailOutput.Token = user.Id;
            //        return Ok(loginEmailOutput);
            //    }
            //}



            loginEmailOutput.Message = "Login successful";
            loginEmailOutput.Status = UserStatus.Active;
            loginEmailOutput.Email = user.Email;
            return Ok(loginEmailOutput);
        }


        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Assuming your production is HTTPS
                SameSite = SameSiteMode.Lax,
            };
            var isProduction_str = Environment.GetEnvironmentVariable("IsProduction");

            // convert string to bool 
            bool isProduction = isProduction_str == "true" ? true : false;

            if (isProduction)
            {
                cookieOptions.Domain = ".mealgenius.ai";
            }
            Response.Cookies.Delete("AuthToken", cookieOptions);

            return Ok(new { message = "Logged out successfully" });
        }



        [HttpPost("Login")]
        public async Task<IActionResult> Login(UserLoginDto userLoginDto)
        {

            // Initialize the response object
            var loginOutput = new LoginOutput();

            ApplicationUser user = null;

            // Regular expression for validating an email address
            string emailRegexPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            bool isEmail = Regex.IsMatch(userLoginDto.Email, emailRegexPattern);

            if (isEmail)
            {
                user = await _userManager.FindByEmailAsync(userLoginDto.Email);
            }
            else
            {
                user = await _userManager.FindByNameAsync(userLoginDto.Email);
            }
            //if user is not found
            if (user == null)
            {
                loginOutput.Message = "User not found.";
                loginOutput.Status = UserStatus.UserNotFound;
                return NotFound(loginOutput);
            }

            var userInputs = _dbcontext.UserInputs.FirstOrDefault(UI => UI.User.Email == user.Email);
            if (userInputs.UserData.IsNullOrEmpty())
            {
                loginOutput.Message = "User data not found, you can register again!";
                loginOutput.Email = user.Email;
                loginOutput.Status = UserStatus.UserNotFound;
                //delete the user 
                _dbcontext.Remove(user);
                return NotFound(loginOutput);
            }


            // Fill in the email for the response
            loginOutput.Email = user.Email;

            // Check if the user is confirmed
            var isEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

            if (!isEmailConfirmed)
            {
                loginOutput.Message = "Email not confirmed, please check your Email to confirm your account";
                loginOutput.Status = UserStatus.AccountNotConfirmed;
                // resend email confirmation
                await _emailService.SendConfirmationEmail(user, user.FirstName ?? "");
                return Ok(loginOutput);
            }

            // Check if the user has a password set
            var isPasswordSet = await _userManager.HasPasswordAsync(user);

            if (!isPasswordSet)
            {
                loginOutput.Message = "Password not Set, please check your Email to reconfirm your account";
                loginOutput.Status = UserStatus.PasswordNotSet;
                loginOutput.Token = user.Id;
                return Ok(loginOutput);
            }


            // Check password validity
            if (!await _userManager.CheckPasswordAsync(user, userLoginDto.Password))
            {
                loginOutput.Message = "Invalid login attempt, wrong password, please try again";
                loginOutput.Status = UserStatus.IncorrectPassword;
                return Ok(loginOutput);
            }
            //check if the user has paid
            var isPaymentConfirmed = user.PaymentConfirmed;
            if (!isPaymentConfirmed)
            {
                loginOutput.Message = "🌟 Hey there! Looks like your payment needs a little nudge to complete. Let’s get you all set up! 🍽️";
                loginOutput.Status = UserStatus.PaymentRequired;
                loginOutput.Token = user.Id;
                return Ok(loginOutput);
            }
            loginOutput.Message = "You have successfully logged in !";
            loginOutput.Email = user.Email;
            var token = await _authService.GenerateToken(user);
            SetAuthTokenCookie(token);

            return Ok(loginOutput);
        }




        #region login-google
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
        #endregion


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

                if (await IsTokenExpired(model.UserId, model.Token))
                {
                    output.Email = user.Email;
                    output.Message = "Token expired.";
                    output.Status = UserStatus.AccountNotConfirmed;
                    output.isExpired = true;
                    return StatusCode(StatusCodes.Status401Unauthorized, output); // 401 for token expired
                }

                // Check if the user has set a password
                var isPasswordSet = await _userManager.HasPasswordAsync(user);
                var isConfirmed = user.EmailConfirmed;


                // if user exist and has password and username, we log in the user
                if (isConfirmed && isPasswordSet)
                {
                    if (!user.PaymentConfirmed)
                    {
                        output.Message = "Your payment has not been confirmed";
                        output.Status = UserStatus.PaymentRequired;
                        output.Email = user.Email;
                        output.Token = user.Id;
                        return Ok(output);
                    }
                    var token = await _authService.GenerateToken(user);
                    SetAuthTokenCookie(token);
                    output.Message = "User activated, Login successfull";
                    output.Status = UserStatus.Active;
                    output.SetConfirmedAt(user.EmailConfirmedAt ?? default(DateTime)); // or some other default value
                    output.Email = user.Email!;
                    return Ok(output);
                }
                // Check if the email is already verified
                else if (isConfirmed)
                {
                    output.Email = user.Email;
                    output.Status = UserStatus.PasswordNotSet;
                    output.SetConfirmedAt(user.EmailConfirmedAt ?? default(DateTime)); // or some other default value
                    output.Message = "Email is already confirmed.";
                    output.isExpired = false;
                    //return StatusCode(StatusCodes.Status409Conflict, output); // 409 for email already verified
                    return Ok(output);
                }

                var confirmResult = await _userManager.ConfirmEmailAsync(user, model.Token);
                if (!confirmResult.Succeeded)
                {
                    _logger.LogWarning("Email confirmation failed for user with ID: {UserId}", model.UserId);
                    output.Message = confirmResult.Errors.FirstOrDefault()?.Description ?? "Error confirming email.";
                    output.Status = UserStatus.PasswordNotSet;
                    if(confirmResult.Errors.FirstOrDefault()?.Code == "InvalidToken")
                    {
                        output.Message = "Invalid token.";
                        return StatusCode(StatusCodes.Status401Unauthorized, output); // 401 for token expired
                    }
                    return BadRequest(output);
                }


                user.EmailConfirmedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user); // Save the change to the database
                output.Message = "Email confirmed.";
                output.Status = UserStatus.PasswordNotSet;
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
        // confirmEmailBackend endpoint 

        [HttpPost("ConfirmEmailbyEmail")]
        public async Task<IActionResult> ConfirmEmailbyEmail([FromBody] EmailConfirmationByEmailModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.email);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // Confirm the email
            user.EmailConfirmed = true;
            user.EmailConfirmedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            // Save changes to the database
            await _dbcontext.SaveChangesAsync();

            return Ok(new { Message = "Email confirmed successfully" });
        }

        private async Task<bool> IsTokenExpired(string userId, string token)
        {
            // where TokenType is the enum value for email confirmation TokenType.EmailConfirmation
            
            var tokenEntry = await _dbcontext.ConfirmationTokens
                .Where(t => t.UserId == userId && t.Token == token && t.TokenType == TokenType.EmailConfirmation)
                .SingleOrDefaultAsync();

            if (tokenEntry == null)
            {
                return true; // Token not found, treat as expired or invalid
            }

            var expiryPeriod = TimeSpan.FromHours(72); // Example: 24 hours
            return DateTime.UtcNow - tokenEntry.IssuedAt > expiryPeriod;
        }

        private void SetAuthTokenCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Adjust based on environment
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(2),
            };
            var isProduction_str = Environment.GetEnvironmentVariable("IsProduction");

            // convert string to bool 
            bool isProduction = isProduction_str == "true" ? true : false;

            if (isProduction)
            {
                cookieOptions.Domain = ".mealgenius.ai";
            }

            Response.Cookies.Append("AuthToken", token, cookieOptions);
        }


        [HttpPost]
        [Route("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // send email check
            await _emailService.SendPasswordResetEmail(user);

            return Ok(new { Message = "Email sent successfully" });
        }




        [HttpPost("ConfirmAccess")]
        public async Task<IActionResult> ConfirmAccess([FromBody] ConfirmAccessModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Token))
                return BadRequest("Invalid request.");


            // where TokenType is the enum value for email confirmation TokenType.PasswordReset
            var tokenEntry = await _dbcontext.ConfirmationTokens
                .Where(t => t.Token == model.Token && t.TokenType == TokenType.ConfirmAccess && t.IssuedAt > DateTime.UtcNow.AddHours(-3)) // assuming 3 hour token validity
                .FirstOrDefaultAsync();

            if (tokenEntry == null)
            {
                return BadRequest("Invalid or expired token.");
            }

            // Find the user by ID obtained from the token
            var user = await _userManager.FindByIdAsync(tokenEntry.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }


            // Optionally, remove the token from the database to prevent reuse
            _dbcontext.ConfirmationTokens.Remove(tokenEntry);
            await _dbcontext.SaveChangesAsync();

            //login user 
            var token = await _authService.GenerateToken(user);
            SetAuthTokenCookie(token);

            return Ok("Access granted");
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
            var email = user.Email;
            return Ok(new 
                    {   Message = "Password setup successful",
                        Email = email
                    });
        }
        [HttpPost]
        [Route("SetupPasswordByEmail")]
        public async Task<IActionResult> SetupPasswordByEmail([FromBody] PasswordSetupByEmailModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            var setPasswordResult = await _userManager.AddPasswordAsync(user, model.Password);
            if (!setPasswordResult.Succeeded)
            {
                return BadRequest(new { ErrorMessage = setPasswordResult.Errors.FirstOrDefault()?.Description ?? "Failed to set password, please try again !" });
            }
            var email = user.Email;
            return Ok(new
            {
                Message = "Password setup successful",
                Email = email
            });
        }

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Token))
                return BadRequest("Invalid request.");

            // Check if the token is valid and find the corresponding user ID

            // where TokenType is the enum value for email confirmation TokenType.PasswordReset
            var tokenEntry = await _dbcontext.ConfirmationTokens
                .Where(t => t.Token == model.Token && t.TokenType == TokenType.PasswordReset && t.IssuedAt > DateTime.UtcNow.AddHours(-1)) // assuming 1 hour token validity
                .FirstOrDefaultAsync();

            if (tokenEntry == null)
            {
                return BadRequest("Invalid or expired token.");
            }

            // Find the user by ID obtained from the token
            var user = await _userManager.FindByIdAsync(tokenEntry.UserId);
            if (user == null)
            {
                return BadRequest("User not found.");
            }

            // If using ASP.NET Core Identity, ResetPasswordAsync isn't suitable here because it expects a token generated by Identity
            // Here we directly change the password since we manage the token ourselves
            var removePasswordResult = await _userManager.RemovePasswordAsync(user);
            if (!removePasswordResult.Succeeded)
            {
                return BadRequest("Failed to reset password.");
            }

            var addPasswordResult = await _userManager.AddPasswordAsync(user, model.Password);
            if (!addPasswordResult.Succeeded)
            {
                return BadRequest(addPasswordResult.Errors);
            }

            // Optionally, remove the token from the database to prevent reuse
            _dbcontext.ConfirmationTokens.Remove(tokenEntry);
            await _dbcontext.SaveChangesAsync();

            //login user 
            var token = await _authService.GenerateToken(user);
            SetAuthTokenCookie(token);

            return Ok("Password has been reset successfully.");
        }

        public record ResetPasswordModel(string Token, string Password);

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
    }


}
