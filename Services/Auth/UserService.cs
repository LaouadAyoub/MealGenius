using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace MealGeniusBackend.Services.Auth
{
    public class UserService : IUserService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;

        public UserService(UserManager<IdentityUser> userManager, IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        // In UserService
        public async Task<(IdentityResult Result, IdentityUser User)> CreateUserAsync(string email, string userName, string password)
        {
            var user = new IdentityUser { Email = email, UserName = userName };
            var result = await _userManager.CreateAsync(user, password);
            return (result, user);
        }

        public async Task<(IdentityResult Result, IdentityUser User)> CreateUserAsync(string iEmail)
        {

            var user = new IdentityUser
            {
                Email = iEmail,
                UserName = iEmail, // Temporarily set the UserName to the email address
                EmailConfirmed = false // This is default, explicitly setting it for clarity
            };

            var result = await _userManager.CreateAsync(user);
            return (result, user);
        }


        public async Task<bool> ConfirmEmailAsync(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            return result.Succeeded;
        }


    }

    public interface IUserService
    {
        Task<(IdentityResult Result, IdentityUser User)> CreateUserAsync(string email);

        Task<(IdentityResult Result, IdentityUser User)> CreateUserAsync(string email, string userName, string password);

        Task<bool> ConfirmEmailAsync(string userId, string token);
    }
}
