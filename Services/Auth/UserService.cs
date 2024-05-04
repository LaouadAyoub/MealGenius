using FluentEmail.Core;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MealGeniusBackend.Services.Auth
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly IEmailService _emailService;
        private readonly UserDbContext _dbcontext;

        public UserService(UserManager<ApplicationUser>  userManager, IEmailService emailService, UserDbContext dbcontext)
        {
            _userManager = userManager;
            _emailService = emailService;
            _dbcontext = dbcontext;
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

        public async Task<bool> UserHasDashboards(ApplicationUser User)
        {

            var existingMealPlan = await _dbcontext.MealPlans.FirstOrDefaultAsync(mealPlan => mealPlan.UserId == User.Id);
            var existingDashboard = await  _dbcontext.UserDashboards.FirstOrDefaultAsync(dashboard => dashboard.UserId == User.Id);

            if (existingDashboard is null && existingMealPlan is null)
                return false;

            if (existingMealPlan.MealPlanJson.IsNullOrEmpty() || existingMealPlan.GroceryListJson.IsNullOrEmpty())
                return false;

            return true;
        
        }

    }

    public interface IUserService
    {
        Task<ApplicationUser> GetUserByIdAsync(string userId);

        Task<(IdentityResult Result, ApplicationUser User)> CreateUserAsync(string email, string userName = "");

        Task<(IdentityResult Result, ApplicationUser User)> CreateUserAsync(string email, string userName, string password);

        Task<ApplicationUser?> GetUserByEmailAsync(string email);

        Task<bool> UserHasDashboards(ApplicationUser User);


    }
}
