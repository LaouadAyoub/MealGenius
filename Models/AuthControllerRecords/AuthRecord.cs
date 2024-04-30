using MealGeniusBackend.Models.Enums;

namespace MealGeniusBackend.Models.AuthControllerRecords
{
    public record ForgotPasswordModel(string Email);

    public record AccountSetupModel(string UserId, string Username, string Password);

    public record PasswordSetupModel(string UserId, string Password);

    public record ConfirmAccessModel(string Token);

    public record UsernameSetupModel(string UserId, string Username);


    public class LoginEmailOut
    {
        public string Message { get; set; }
        public UserStatus Status { get; set; }

        public string Email { get; set; }
    }

    public class LoginOutput
    {
        public string Email { get; set; }
        public string Message { get; set; }
        public bool IsEmailConfirmed { get; set; }
        //has the user paid or not?
        public bool IsPaymentConfirmed { get; set; }
        public bool IsPasswordSet { get; set; }
    }
}
