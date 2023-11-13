using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;

namespace MealGeniusBackend.DataAcess
{
    public class UserDbContext : IdentityDbContext
    {
        public UserDbContext(DbContextOptions<UserDbContext> options)
            : base(options)
        { }

        public DbSet<UserTask> Tasks { get; set; }
        public DbSet<UserInput> UserInputs { get; set; }
        public DbSet<MealPlan> MealPlans { get; set; }
        public DbSet<UserDashboard> UserDashboards { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // UserTask to UserDashboard one-to-one relationship
            builder.Entity<UserTask>()
                .HasOne(t => t.UserDashboard)
                .WithOne(d => d.Task)
                .HasForeignKey<UserDashboard>(d => d.TaskId);

            // UserTask to UserInput one-to-one relationship
            builder.Entity<UserTask>()
                .HasOne(t => t.UserInput)
                .WithOne(i => i.Task)
                .HasForeignKey<UserInput>(i => i.TaskId);

            // UserTask to MealPlan one-to-one relationship
            builder.Entity<UserTask>()
                .HasOne(t => t.MealPlan)
                .WithOne(m => m.Task)
                .HasForeignKey<MealPlan>(m => m.TaskId);

            // Constraints on UserId in MealPlan, UserInput, UserDashboard to match IdentityUser
            builder.Entity<UserDashboard>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId);

            builder.Entity<UserInput>()
                .HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId);

            builder.Entity<MealPlan>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId);
        }
    }


    public class UserDashboard
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public Guid TaskId { get; set; }
        public string SummarySection { get; set; }
        public string BmrInitialContent { get; set; }
        public string BmrExpandedText { get; set; }
        public string CaloricNeedsInitialContent { get; set; }
        public string CaloricNeedsExpandedText { get; set; }
        public string UserDetails { get; set; }

        public IdentityUser User { get; set; }
        public UserTask Task { get; set; }
    }
    public class UserTask
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }  // This matches the IdentityUser Id type
        public UserTaskStatus Status { get; set; }  // Changed from string to TaskStatus enum

        public IdentityUser User { get; set; }

        // Navigation property
        public UserDashboard UserDashboard { get; set; } 
        public UserInput UserInput { get; set; } 
        public MealPlan MealPlan { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }

    public enum UserTaskStatus
    {
        New,
        InProgress,
        Completed,
        Failed
    }


    public class UserInput
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public Guid TaskId { get; set; }
        public string UserData { get; set; }  // Storing JSON as string

        public IdentityUser User { get; set; }
        public UserTask Task { get; set; }
    }
    public class MealPlan
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public Guid TaskId { get; set; }
        public UserTask Task { get; set; }

        public string Title { get; set; }

        [Column(TypeName = "jsonb")]  // 🌟 This is where the magic happens!
        public string MealPlanJson { get; set; }
        public string GroceryListJson { get; set; } // Added this line for the grocery list
        public IdentityUser User { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }



}