using Microsoft.AspNetCore.Identity;

namespace MealGeniusBackend.Models.UserModel
{
    public class ApplicationUser : IdentityUser
    {
        public ICollection<MealPlanDb> MealPlans { get; set; }

    }

}
