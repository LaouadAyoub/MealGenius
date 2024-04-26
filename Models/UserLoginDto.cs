namespace MealGeniusBackend.Models
{
    public record UserLoginDto(string Email, string Password);

    public record LoginEmailDto(string EmailOrUsername);

}
