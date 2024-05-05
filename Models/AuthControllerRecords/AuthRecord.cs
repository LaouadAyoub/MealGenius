using MealGeniusBackend.Models.Enums;

namespace MealGeniusBackend.Models.AuthControllerRecords
{
    public record ForgotPasswordModel(string Email);

    public record AccountSetupModel(string UserId, string Username, string Password);

    public record PasswordSetupModel(string UserId, string Password);

    public record PasswordSetupByEmailModel(string Email, string Password);


    public record ConfirmAccessModel(string Token);

    public record UsernameSetupModel(string UserId, string Username);


    public class ConfirmAccessOut
    {
        public string Message { get; set; }
        public UserStatus Status { get; set; }

        public string Email { get; set; }
        public string Token { get; set; } = "";
    }

    public class ResetPasswordOut
    {
        public string Message { get; set; }
        public UserStatus Status { get; set; }

        public string Email { get; set; }
        public string Token { get; set; } = "";
    }
    public class LoginEmailOut
    {
        public string Message { get; set; }
        public UserStatus Status { get; set; }

        public string Email { get; set; }
        public string Token { get; set; } = "";
    }

    public class LoginOutput
    {
        public string Email { get; set; }
        public string Message { get; set; }

        public UserStatus Status { get; set; }

        public string Token { get; set; } = "";
    }


    public class RegisterOut
    {
        public string Email { get; set; }

        public string Message { get; set; }
        public UserStatus Status { get; set; }
        public string Token { get; set; } = "";

    }
}
