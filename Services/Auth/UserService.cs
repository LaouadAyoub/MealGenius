using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace MealGeniusBackend.Services.Auth
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly IEmailService _emailService;

        public UserService(UserManager<ApplicationUser>  userManager, IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        // In UserService
        public async Task<(IdentityResult Result, ApplicationUser User)> CreateUserAsync(string email, string userName, string password)
        {
            var user = new ApplicationUser { Email = email, UserName = userName };
            var result = await _userManager.CreateAsync(user, password);
            return (result, user);
        }

        public async Task<(IdentityResult Result, ApplicationUser User)> CreateUserAsync(string iEmail, string iFirstName = "")
        {

            var user = new ApplicationUser
            {
                Email = iEmail,
                UserName = iEmail, // Temporarily set the UserName to the email address
                EmailConfirmed = false, // This is default, explicitly setting it for clarity
                FirstName = iFirstName,
            };

            var result = await _userManager.CreateAsync(user);
            return (result, user);
        }

        public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user;
        }
        public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user;
        }

    }

    public interface IUserService
    {
        Task<ApplicationUser> GetUserByIdAsync(string userId);

        Task<(IdentityResult Result, ApplicationUser User)> CreateUserAsync(string email, string userName = "");

        Task<(IdentityResult Result, ApplicationUser User)> CreateUserAsync(string email, string userName, string password);

        Task<ApplicationUser?> GetUserByEmailAsync(string email);

    }
}
