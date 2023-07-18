namespace MealGeniusBackend.Models.UserModel
{
    public class MealPlanDb
    {
        public string Id { get; set; }

        // This is a foreign key
        public string UserId { get; set; }

        public string PlanDetails { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public ApplicationUser User { get; set; }
    }

}
