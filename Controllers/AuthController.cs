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

            var user = await _userManager.FindByNameAsync(userLoginDto.Username);
            
            if (user == null || !await _userManager.CheckPasswordAsync(user, userLoginDto.Password))
            {
                return Unauthorized();
            }

            var token = await _authService.GenerateToken(user);
            return Ok(new { Token = token });
        }
    }
}
