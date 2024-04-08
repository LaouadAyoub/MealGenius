using FluentEmail.Core;
using FluentEmail.Core.Models;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace MealGeniusBackend.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmail(ApplicationUser user, string name);

    }

    public partial class EmailService : IEmailService
    {
        private readonly FluentEmail.Core.IFluentEmailFactory _emailFactory;
        private readonly ILogger<EmailService> _logger;
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly UserDbContext _dbcontext;



        public EmailService(IFluentEmailFactory emailFactory, ILogger<EmailService> logger, UserManager<ApplicationUser>  userManager, UserDbContext dbcontext)
        {
            _emailFactory = emailFactory;
            _logger = logger;
            _userManager = userManager;
            _dbcontext = dbcontext;
        }
        public async Task SendConfirmationEmail(ApplicationUser user, string name)
        {
            try
            {
                _logger.LogInformation("Generating email confirmation token.");
                var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                _dbcontext.ConfirmationTokens.Add(new ConfirmationToken
                {
                    UserId = user.Id,
                    Token = confirmationToken,
                    IssuedAt = DateTime.UtcNow
                });
                await _dbcontext.SaveChangesAsync();
                _logger.LogInformation("Confirmation token generated and saved.");

                var confirmationLink = BuildConfirmationLink(user.Id, confirmationToken);
                var htmlTemplate = LoadEmailTemplate();
                htmlTemplate = CustomizeEmailTemplate(htmlTemplate, name, confirmationLink);

                await SendEmail(user.Email, htmlTemplate);
                _logger.LogInformation("Confirmation email sent successfully.");
                user.ConfirmationEmailSentAt = DateTime.UtcNow;
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

        private string BuildConfirmationLink(string userId, string token)
        {
            var frontendBaseUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";
            var confirmationPageRoute = Environment.GetEnvironmentVariable("CONFIRMATION_PAGE_ROUTE") ?? "/confirm-account";
            return $"{frontendBaseUrl}{confirmationPageRoute}?userId={userId}&token={Uri.EscapeDataString(token)}";
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

        private string CustomizeEmailTemplate(string template, string name, string confirmationLink)
        {
            return template.Replace("{Name}", name).Replace("{confirmationLink}", confirmationLink);
        }

        private async Task SendEmail(string toEmail, string htmlTemplate)
        {
            var email = _emailFactory.Create()
                .To(toEmail)
                .Subject("MealGenius Email Confirmation")
                .Body(htmlTemplate, isHtml: true);

            var response = await email.SendAsync();
            if (!response.Successful)
            {
                _logger.LogError($"Failed to send confirmation email: {string.Join(", ", response.ErrorMessages)}");
                throw new Exception($"Failed to send confirmation email: {string.Join(", ", response.ErrorMessages)}");
            }
        }




    }
}
