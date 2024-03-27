using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Identity;

namespace MealGeniusBackend.DataAccess
{
    public class UserDbContextSeeder
    {
        private readonly UserDbContext _context;
        private readonly UserManager<ApplicationUser>  _userManager;

        public UserDbContextSeeder(UserDbContext context, UserManager<ApplicationUser>  userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task SeedDatabase()
        {
            // If the database doesn't contain any users, create a default user
            if (!_context.Users.Any())
            {
                var user = new ApplicationUser
                {
                    UserName = "ayoub",
                    Email = "redacted@example.invalid"
                };
                var result = await _userManager.CreateAsync(user, "REDACTED");
                if (!result.Succeeded)
                {
                    // Log the errors
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"{error.Code}: {error.Description}");
                    }
                }
            }
        }
    }

}
