using MealGeniusBackend.Models.Enums;

namespace MealGeniusBackend.Models.ModelsControllers
{

    public class EmailConfirmationOutput
    {
        public string Message { get; set; }
        public UserStatus Status { get; set; }
        public DateTime ConfirmedAt { get; set; }
        public bool isExpired { get; set; }
        public string Email { get; set; }

        public string Token { get; set; }


        // Public method to set ConfirmedAt
        public void SetConfirmedAt(DateTime confirmedAtTime)
        {
            ConfirmedAt = confirmedAtTime;
        }

    }

}
