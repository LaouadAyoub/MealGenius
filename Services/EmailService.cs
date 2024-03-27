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

            // Generate the confirmation token
            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            _dbcontext.ConfirmationTokens.Add(new ConfirmationToken
            {
                UserId = user.Id,
                Token = confirmationToken,
                IssuedAt = DateTime.UtcNow
            });

            await _dbcontext.SaveChangesAsync();
            // Build the confirmation link
            var frontendBaseUrl = "http://localhost:3000";
            var confirmationPageRoute = Environment.GetEnvironmentVariable("CONFIRMATION_PAGE_ROUTE") ?? "/confirm-account";
            var confirmationLink = $"{frontendBaseUrl}{confirmationPageRoute}?userId={user.Id}&token={Uri.EscapeDataString(confirmationToken)}";

            // Load your HTML template as a string
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "emailTemplate.html");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The file {filePath} was not found.");
            }

            string htmlTemplate = File.ReadAllText(filePath);

            // Replace placeholders in the HTML template
            htmlTemplate = htmlTemplate.Replace("{Name}", name);
            htmlTemplate = htmlTemplate.Replace("{confirmationLink}", confirmationLink);

            // Prepare and send the email
            var email = _emailFactory.Create()
                .To(user.Email)
                .Subject("MealGenius Email confirmation")
                .Body(htmlTemplate, isHtml: true);

            var response = await email.SendAsync();
            if (!response.Successful)
            {
                // Handle the error appropriately
                throw new Exception($"Failed to send confirmation email: {string.Join(", ", response.ErrorMessages)}");
            }
        }


        // Method to load the HTML template as a string (this is a placeholder, implement accordingly)
        private string LoadTemplateFromFile(string filePath)
        {
            // Your method to load the HTML content from a file
            return System.IO.File.ReadAllText(filePath);
        }

    }
}
