using FluentEmail.Core;
using FluentEmail.Core.Models;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MealGeniusBackend.Services.Auth
{
    // AuthService.cs (in your Services folder)
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly IConfiguration _configuration;
        private readonly JwtSecurityTokenHandler _tokenHandler;

        public AuthService(UserManager<ApplicationUser>  userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
            _tokenHandler = new JwtSecurityTokenHandler();
        }

        public async Task<string> GenerateToken(IdentityUser user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim("security_stamp", user.SecurityStamp!),
                new Claim("paid", user is MealGeniusBackend.Models.ApplicationUser appUser && appUser.PaymentConfirmed ? "true" : "false")
            };

            var jwtKey = _configuration["JwtConfig:Key"];

            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JwtKey is not set in the configuration.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = signingCredentials,
                Issuer = _configuration["JwtConfig:Issuer"],
                Audience = _configuration["JwtConfig:Audience"],
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            return _tokenHandler.WriteToken(token);
        }
    }

    // IAuthService.cs (in your Interfaces folder)
    public interface IAuthService
    {
        Task<string> GenerateToken(IdentityUser user);
    }

}
