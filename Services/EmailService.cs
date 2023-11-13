using FluentEmail.Core;
using FluentEmail.Core.Models;
using MealGeniusBackend.DataAcess;
using System.Security.Cryptography;

namespace MealGeniusBackend.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmail(string toEmail, string confirmationLink);

    }

    public partial class EmailService : IEmailService
    {
        private readonly FluentEmail.Core.IFluentEmailFactory _emailFactory;
        private readonly ILogger<EmailService> _logger;


        public EmailService(IFluentEmailFactory emailFactory, ILogger<EmailService> logger)
        {
            _emailFactory = emailFactory;
            _logger = logger;
        }
        public async Task SendConfirmationEmail(string toEmail, string confirmationLink)
        {
            var email = _emailFactory.Create()
                .To(toEmail)
                .Subject("Confirm Your Email")
                .Body($"Please confirm your email by clicking <a href=\"{confirmationLink}\">here</a>.", true);

            var response = await email.SendAsync();

            if (!response.Successful)
            {
                // Log the error messages
                // Consider how you want to handle this: re-throw, store for retry, etc.
                throw new Exception($"Failed to send confirmation email: {string.Join(", ", response.ErrorMessages)}");
            }
        }

    }
}
