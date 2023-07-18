using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MealGeniusBackend.Models.UserModel;

namespace MealGeniusBackend.DataAcess
{
    public class UserDbContext : IdentityDbContext<ApplicationUser>
    {
        public UserDbContext(DbContextOptions<UserDbContext> options)
            : base(options)
        { }

        // Add a DbSet for MealPlans
        public DbSet<MealPlanDb> MealPlans { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure the relationship between ApplicationUser and MealPlan
            builder.Entity<MealPlanDb>()
                .HasOne(mp => mp.User)
                .WithMany(u => u.MealPlans)
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
