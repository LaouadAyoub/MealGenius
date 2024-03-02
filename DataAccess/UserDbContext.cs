using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs.Models;

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
        public DbSet<GroceryItem> GroceryItems { get; set; }  // Ajout du nouveau DbSet
        //ADD NotFoundGroceryItems as a new table a new DbSet
        public DbSet<NotFoundGroceryItems> NotFoundGroceryItems { get; set; }

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

            // Configuration pour GroceryItem
            builder.Entity<GroceryItem>(entity =>
            {
                entity.ToTable("GroceryItems");
                entity.HasKey(e => e.GroceryItemId);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.SimilarNames);  // La configuration dépend de vos besoins
                entity.Property(e => e.Category).IsRequired();
                entity.Property(e => e.ImageUrl).IsRequired();
            });

            builder.Entity<NotFoundGroceryItems>(entity =>
            {
                entity.ToTable("NotFoundGroceryItems");
                entity.HasKey(e => e.GroceryItemId);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Category).IsRequired();
            });
        }
    }


    public class UserDashboard
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public Guid TaskId { get; set; }
        public string UserGoalsGuide { get; set; }
        public string MacroTargets { get; set; }
        public string MicroGuide { get; set; }
        public string WaterIntake { get; set; }
        public string JsonUserKeyInfos { get; set; }
        public IdentityUser User { get; set; }
        public UserTask Task { get; set; }
    }

    public class UserTask
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }  // This matches the IdentityUser Id type
        public UserTaskStatus Status { get; set; }  // Changed from string to TaskStatus enum

        public UserMealsImagesStatus MealsImagesStatus { get; set; }

        public UserOutputStatus UserOutputStatus { get; set; }

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
        Completed,
        Failed
    }

    public enum UserOutputStatus
    {
        New,
        DashboardCompleted,
        MealPlanCompleted,
        GroceryListCompleted
    }
    public enum UserMealsImagesStatus
    {
        NotStarted,
        Ongoing,
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

    public class GroceryItem
    {
        public Guid GroceryItemId { get; set; }
        public string Name { get; set; }  // Nom principal de l'article d'épicerie, e.g., "Whey Protein"
        public List<string> SimilarNames { get; set; }  // Noms similaires, e.g., "Protein Powder, Vanilla Whey Protein"
        public string Category { get; set; }  // Catégorie de l'article, e.g., "Supplements"
        public string ImageUrl { get; set; }  // URL de l'image de l'article
    }

    public class NotFoundGroceryItems
    {
        public Guid GroceryItemId { get; set; }
        public string Name { get; set; } 
        public string Category { get; set; }
        public List<string> SimilarNames { get; set; }  // Noms similaires, e.g., "Protein Powder, Vanilla Whey Protein"

        public string SimilarGroceryItemFound { get; set; }  // Ajout de cette propriété pour stocker le nom de l'article d'épicerie similaire trouvé
    }
}