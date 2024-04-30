namespace MealGeniusBackend.Models.ModelsControllers
{

    public class EmailConfirmationOutput
    {
        public string Message { get; set; }

        public bool isConfirmed { get; set; }

        public DateTime ConfirmedAt { get; set; }
        public bool isExpired { get; set; }
        public string Email { get; set; }

        public bool isPasswordSet { get; set; }

        //public bool isUsernameSet { get; set; }

        // Public method to set ConfirmedAt
        public void SetConfirmedAt(DateTime confirmedAtTime)
        {
            ConfirmedAt = confirmedAtTime;
        }

    }

}
