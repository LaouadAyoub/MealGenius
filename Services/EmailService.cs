using MealGeniusBackend.Services.Auth;
using System.Net;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace MealGeniusBackend.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmail(ApplicationUser user, string name);
        Task SendPaymentConfirmationEmail(string email);
        Task SendPasswordResetEmail(ApplicationUser user);

    }

    public partial class EmailService : IEmailService
    {
        private readonly FluentEmail.Core.IFluentEmailFactory _emailFactory;
        private readonly ILogger<EmailService> _logger;
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly UserDbContext _dbcontext;
        private readonly IConfiguration _configuration;



        public EmailService(IFluentEmailFactory emailFactory, ILogger<EmailService> logger, UserManager<ApplicationUser>  userManager, UserDbContext dbcontext, IConfiguration configuration)
        {
            _emailFactory = emailFactory;
            _logger = logger;
            _userManager = userManager;
            _dbcontext = dbcontext;
            _configuration = configuration;
        }
        public async Task SendConfirmationEmail(ApplicationUser user, string name)
        {
            try
            {
                _logger.LogInformation("Generating email confirmation token.");
                var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                _logger.LogInformation("Email token generated.");

                var confirmationLink = BuildConfirmationLink(user.Id, confirmationToken);
                var htmlTemplate = LoadEmailTemplate();
                htmlTemplate = CustomizeEmailTemplate(htmlTemplate, name, confirmationLink);

                await SendEmail(user.Email, "MealGenius Email Confirmation", htmlTemplate);
                _logger.LogInformation("Confirmation email sent successfully.");
                user.ConfirmationEmailSentAt = DateTime.UtcNow;
                await _dbcontext.SaveChangesAsync();
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Email template file not found.");
                throw new InvalidOperationException("The email confirmation process failed due to a missing template.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email.");
                throw new InvalidOperationException("An unexpected error occurred while sending the confirmation email.", ex);
            }
        }
        public async Task SendPasswordResetEmail(ApplicationUser user)
        {
            try
            {

                _logger.LogInformation("Generating email confirmation token.");

                if (user.Email.IsNullOrEmpty())
                {
                    return;
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                _logger.LogInformation("Email token generated.");

                var confirmationLink = BuildPasswordResetLink(user.Id, token);
                var htmlTemplate = LoadPasswordResetTemplate();

                var userFirstName = user.FirstName ?? user.Email;
                htmlTemplate = CustomizePasswordResetEmailTemplate(htmlTemplate, userFirstName,  confirmationLink);

                string emailSubject = "MealGenius Password Reset";

                await SendEmail(user.Email!, emailSubject, htmlTemplate);
                _logger.LogInformation("Confirmation email sent successfully.");
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Email template file not found.");
                throw new InvalidOperationException("The email confirmation process failed due to a missing template.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email.");
                throw new InvalidOperationException("An unexpected error occurred while sending the confirmation email.", ex);
            }
        }

        public async Task SendPaymentConfirmationEmail(string email)
        {
            try
            {
                _logger.LogInformation("Generating email confirmation token.");
                var user = await _userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found.");
                var token = GenerateSecureToken();
                _dbcontext.ConfirmationTokens.Add(new ConfirmationToken
                {
                    UserId = user.Id,
                    Token = TokenHash.Compute(token),
                    IssuedAt = DateTime.UtcNow,
                    TokenType = TokenType.ConfirmAccess
                });
                await _dbcontext.SaveChangesAsync();
                _logger.LogInformation("Email token generated.");

                var userFirstName = user?.FirstName ?? "";

                var htmlTemplate = LoadPaymentConfirmationTemplate();


                var confirmationLink = BuildConfirmPaymentLink(token);

 
                htmlTemplate = CustomizePaymentConfirmationEmailTemplate(htmlTemplate, userFirstName, confirmationLink);

                string emailSubject = "MealGenius Payment Confirmation";
                await SendEmail(email, emailSubject, htmlTemplate);
                _logger.LogInformation("Payment confirmation email sent successfully.");

            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Email template file not found.");
                throw new InvalidOperationException("The email payment confirmation process failed due to a missing template.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email.");
                throw new InvalidOperationException("An unexpected error occurred while sending the Payment confirmation email.", ex);
            }
        }

        private string BuildPasswordResetLink(string userId, string token)
        {
            var frontendBaseUrl = _configuration["FRONTEND_URL"] ?? "http://localhost:3000";
            var confirmationPageRoute = "/reset-password";
            return $"{frontendBaseUrl}{confirmationPageRoute}?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
        }

        private string BuildConfirmPaymentLink(string token)
        {
            var frontendBaseUrl = _configuration["FRONTEND_URL"] ?? "http://localhost:3000";
            var confirmationPageRoute = "/confirm-payment";
            return $"{frontendBaseUrl}{confirmationPageRoute}?token={Uri.EscapeDataString(token)}";
        }
        private string BuildConfirmationLink(string userId, string token)
        {
            var frontendBaseUrl = _configuration["FRONTEND_URL"] ?? "http://localhost:3000";
            var confirmationPageRoute = _configuration["CONFIRMATION_PAGE_ROUTE"] ?? "/confirm-account";
            return $"{frontendBaseUrl}{confirmationPageRoute}?userId={userId}&token={Uri.EscapeDataString(token)}";
        }
        //passwordTemplate.html
        private string LoadPasswordResetTemplate()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "passwordTemplate.html");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file {filePath} was not found.");
            }

            return File.ReadAllText(filePath);
        }

        private string LoadPaymentConfirmationTemplate()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "paymentConfirmationTemplate.html");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file {filePath} was not found.");
            }

            return File.ReadAllText(filePath);
        }

        private string LoadEmailTemplate()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "emailTemplate.html");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file {filePath} was not found.");
            }

            return File.ReadAllText(filePath);
        }

        private string CustomizePasswordResetEmailTemplate(string template, string name, string resetLink)
        {
           return template.Replace("{Name}", WebUtility.HtmlEncode(name)).Replace("{resetLink}", resetLink);

        }


        private string CustomizePaymentConfirmationEmailTemplate(string template, string name, string paymentConfirmationLink)
        {
            return template.Replace("{Name}", WebUtility.HtmlEncode(name)).Replace("{paymentConfirmationLink}", paymentConfirmationLink);

        }

        private string CustomizeEmailTemplate(string template, string name, string confirmationLink)
        {
            return template.Replace("{Name}", WebUtility.HtmlEncode(name)).Replace("{confirmationLink}", confirmationLink);
        }

        private async Task SendEmail(string toEmail,string subject, string htmlTemplate)
        {
            var email = _emailFactory.Create()
                .To(toEmail)
                .Subject(subject)
                .Body(htmlTemplate, isHtml: true);

            var response = await email.SendAsync();
            if (!response.Successful)
            {
                _logger.LogError($"Failed to send email: {string.Join(", ", response.ErrorMessages)}");
                throw new Exception($"Failed to send email: {string.Join(", ", response.ErrorMessages)}");
            }
        }

        private string GenerateSecureToken()
        {
            return Guid.NewGuid().ToString("N") + GenerateRandomString(32);
        }

        private string GenerateRandomString(int length)
        {
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                var randomBytes = new byte[length];
                randomNumberGenerator.GetBytes(randomBytes);
                return Convert.ToBase64String(randomBytes);
            }
        }


    }
}
