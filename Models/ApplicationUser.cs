using Microsoft.AspNetCore.Identity;

namespace MealGeniusBackend.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public DateTime? EmailConfirmedAt { get; set; }
        public DateTime? UserCreatedAt { get; set; }

        public DateTime? ConfirmationEmailSentAt { get; set; }

        public bool PaymentConfirmed { get; set; }

        public DateTime? PaymentConfirmedAt { get; set; }
    }
}
