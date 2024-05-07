using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;

namespace MealGeniusBackend.Controllers
{
    [Route("api/stripe")]
    [ApiController]
    public class StripeWebhookController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly IUserService _userService; // You might need user service to fetch user data if required
        private readonly ILogger<StripeWebhookController> _logger; // Ensure ILogger is injected
        private readonly IExecuteTaskService _executeTaskService;
        private readonly UserDbContext _dbcontext;

        public StripeWebhookController(IConfiguration configuration, IEmailService emailService, IUserService userService, ILogger<StripeWebhookController> logger, IExecuteTaskService executeTaskService, UserDbContext dbcontext)
        {
            _emailService = emailService;
            _userService = userService;
            _logger = logger;
            _executeTaskService = executeTaskService;
            _dbcontext = dbcontext;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            var endpointSecret = Environment.GetEnvironmentVariable("EndpointSecret");

            if (string.IsNullOrEmpty(endpointSecret))
            {
                // Fallback secret; should ideally be retrieved securely
                endpointSecret = "REDACTED";
                _logger.LogWarning("Endpoint secret is null or empty. Using fallback.");
            }

            _logger.LogInformation("I am inside the handle of the webhook");

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    endpointSecret,
                    throwOnApiVersionMismatch: false
                );

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if (session.PaymentStatus == "paid")
                    {
                        var customerEmail = session.CustomerDetails.Email; // Assuming email is collected
                        var clientReferenceId = session.ClientReferenceId;

                        ApplicationUser user = null;
                        if (!string.IsNullOrEmpty(clientReferenceId))
                        {
                            user = await _userService.GetUserByIdAsync(clientReferenceId);
                        }

                        if (user == null) // No user found with clientReferenceId, or clientReferenceId is null/empty
                        {
                            user = await _userService.GetUserByEmailAsync(customerEmail);
                        }

                        if (user != null)
                        {
                            user.PaymentConfirmed = true;
                            user.PaymentConfirmedAt = DateTime.UtcNow;
                            _logger.LogInformation($"Database updated with payment confirmation for user {user.Email}.");
                        }
                        else
                        {
                            // Create a new user if not found by id or email
                            var (result, newUser) = await _userService.CreateUserAsync(customerEmail);
                            if (result.Succeeded)
                            {
                                newUser.PaymentConfirmed = true;
                                newUser.PaymentConfirmedAt = DateTime.UtcNow;
                                user = newUser; // Update user reference to new user
                                _logger.LogInformation($"New user created and payment confirmed for {newUser.Email}.");
                            }
                        }

                        await _dbcontext.SaveChangesAsync(); // Save changes once for any user scenario

                        if (user != null) // Check if user object is properly instantiated
                        {
                            // Send payment confirmation email
                            await SendPaymentConfirmationEmail(user.Email); // Send email to the confirmed user's email
                            _logger.LogInformation($"Payment confirmation email sent to {user.Email}.");
                            var existingUserInputs = await _dbcontext.UserInputs.FirstOrDefaultAsync(inputs => inputs.UserId == user.Id);

                            if (existingUserInputs is not null && !existingUserInputs.UserData.IsNullOrEmpty())
                            {
                                // Execute task for confirmed user
                                await _executeTaskService.ExecuteUserTask(user.Email);
                            }

                        }
                    }
                }

                return Ok();
            }
            catch (StripeException e)
            {
                _logger.LogError($"Stripe exception: {e.Message}");
                return BadRequest(e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError($"General exception: {e.Message}");
                return StatusCode(500, e.Message);
            }
        }


        private async Task SendPaymentConfirmationEmail(string email)
        {
            try
            {
                // Call your email service to send a confirmation email
                await _emailService.SendPaymentConfirmationEmail(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payment confirmation email.");
                throw; // Consider handling this more gracefully depending on your business logic
            }
        }
    }
}
