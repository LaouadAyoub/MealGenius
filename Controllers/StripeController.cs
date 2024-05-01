using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using Microsoft.AspNetCore.Mvc;
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

        public StripeWebhookController(IConfiguration configuration, IEmailService emailService, IUserService userService, ILogger<StripeWebhookController> logger, IExecuteTaskService executeTaskService)
        {
            _emailService = emailService;
            _userService = userService;
            _logger = logger;
            _executeTaskService = executeTaskService;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            var endpointSecret = Environment.GetEnvironmentVariable("EndpointSecret");

            if (endpointSecret.IsNullOrEmpty())
            {
                //REDACTED
                endpointSecret = "REDACTED";
                _logger.LogWarning("endpoint Secret is null !!!!!!!!!!");
            }


            _logger.LogInformation($"I am inside the handle of the webhook");
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
                    string customerEmail;
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                    if (session.PaymentStatus == "paid")
                    {
                        customerEmail = session.CustomerDetails.Email; // Assuming email is collected

                        var clientReferenceId = session.ClientReferenceId;
                        if (!clientReferenceId.IsNullOrEmpty())
                        {
                            var user = await _userService.GetUserByIdAsync(clientReferenceId);
                            if (user != null)
                            {
                                user.PaymentConfirmed = true;
                                customerEmail = user.Email!;
                            }
                            else
                            {
                                _logger.LogInformation($"User with id {clientReferenceId} not found.");
                                
                                var userByEmail = await _userService.GetUserByEmailAsync(customerEmail);
                                if (userByEmail != null)
                                {
                                    userByEmail.PaymentConfirmed = true;
                                }
                                else
                                { 
                                    // if the user is not found by id or email, you can create a new user here
                                    var (result, newUser) = await _userService.CreateUserAsync(customerEmail);
                                    if (result.Succeeded)
                                    {
                                        newUser.PaymentConfirmed = true;
                                    }
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(customerEmail))
                        {
                            // Send payment confirmation email
                            await _executeTaskService.ExecuteUserTask(customerEmail);
                            await SendPaymentConfirmationEmail(customerEmail);

                            _logger.LogInformation($"Payment confirmation email sent to {customerEmail}.");
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
