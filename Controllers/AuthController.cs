using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MealGeniusBackend.Controllers
{
    /// <summary>
    /// AuthController
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class AuthController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IAuthService _authService;

        public AuthController(UserManager<IdentityUser> userManager, IAuthService authService)
        {
            _userManager = userManager;
            _authService = authService;
        }

        /// <summary>
        /// Login method to authenticate user
        /// </summary>
        /// <param name="userLoginDto"></param>
        /// <returns></returns>

        [HttpPost("Login")]
        public async Task<IActionResult> Login(UserLoginDto userLoginDto)
        {
            IdentityUser user = null;

            // Check if the input is an email
            if (userLoginDto.Username.Contains("@"))
            {
                user = await _userManager.FindByEmailAsync(userLoginDto.Username);
            }
            else
            {
                user = await _userManager.FindByNameAsync(userLoginDto.Username);
            }

            if (user == null || !await _userManager.CheckPasswordAsync(user, userLoginDto.Password))
            {
                return Unauthorized();
            }
            var token = await _authService.GenerateToken(user);

            // Set the token in an HttpOnly cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Set to true if using HTTPS. If you're in development (likely using HTTP), this can be set based on the request or environment.
                SameSite = SameSiteMode.Strict, // Helps mitigate CSRF. Consider Lax if you need cross-site requests.
                Expires = DateTime.UtcNow.AddDays(1), // Align with your token's expiration
            };

            Response.Cookies.Append("AuthToken", token, cookieOptions);

            // Optionally return a simple message indicating success
            return Ok(new { Message = "Login successful" });
        }

    }
}
