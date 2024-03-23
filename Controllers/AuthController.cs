using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;
        private readonly UserDbContext _dbcontext;

        public AuthController(UserManager<IdentityUser> userManager, IAuthService authService,
                                ILogger<AuthController> logger,
                                IEmailService emailService, IUserService userService, UserDbContext dbcontext, SignInManager<IdentityUser> signInManager)
        {
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

            IdentityUser user = null;

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
                return BadRequest(new { ErrorMessage = "Failed to set password.", Errors = setPasswordResult.Errors });
            }

            return Ok(new { Message = "Password setup successful" });
        }

        [HttpPost]
        [Route("SetupUsernameAndLogin")]
        public async Task<IActionResult> SetupUsernameAndLogin([FromBody] UsernameSetupModel model)
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

            user.UserName = model.Username;
            var setUsernameResult = await _userManager.UpdateAsync(user);
            if (!setUsernameResult.Succeeded)
            {
                return BadRequest(new { ErrorMessage = "Failed to set username.", Errors = setUsernameResult.Errors });
            }

            var token = await _authService.GenerateToken(user);
            SetAuthTokenCookie(token);

            return Ok(new { Message = "Username setup and login successful" });
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
