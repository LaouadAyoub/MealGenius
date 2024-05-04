namespace MealGeniusBackend.Models.ModelsControllers
{

    public class EmailConfirmationModel
    {
        public string UserId { get; set; }
        public string Token { get; set; }
    }
    public class EmailConfirmationByEmailModel
    {
        public string email { get; set; }
    }

}
