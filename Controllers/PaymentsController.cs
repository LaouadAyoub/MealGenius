using MealGeniusBackend.DataAcess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace MealGeniusBackend.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly UserDbContext _dbContext;
        
        public PaymentsController(UserDbContext context)
        {
            _dbContext = context;
        }
        [HttpPost]
        [Route("create-payment-intent")]
        public IActionResult CreatePaymentIntent([FromBody] PaymentRequest request)
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount, // Amount in cents. For $10.00, set this to 1000.
                Currency = "usd", // Use the appropriate currency.
            };

            var service = new PaymentIntentService();
            PaymentIntent paymentIntent = service.Create(options);

            return Ok(new { paymentIntent.Id, paymentIntent.ClientSecret });
        }

        //[HttpPost]
        //[Route("confirm-payment")]
        //public IActionResult ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        //{
        //    var service = new PaymentIntentService();
        //    var confirmOptions = new PaymentIntentConfirmOptions
        //    {
        //        PaymentMethod = request.PaymentMethodId
        //    };

        //    PaymentIntent confirmedPaymentIntent = service.Confirm(request.PaymentIntentId, confirmOptions);

        //    return Ok(confirmedPaymentIntent.Status);
        //}

        [HttpPost]
        [Route("confirm-payment")]
        public IActionResult ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            var service = new PaymentIntentService();
            var confirmOptions = new PaymentIntentConfirmOptions
            {
                PaymentMethod = request.PaymentMethodId
            };

            PaymentIntent confirmedPaymentIntent = service.Confirm(request.PaymentIntentId, confirmOptions);

            // Check if the payment is successful
            if (confirmedPaymentIntent.Status == "succeeded")
            {
                try
                {
                    // 1. Create a new unverified user
                    //var newUser = CreateUnverifiedUser(request.Email);  // This is pseudo-code. Implement this function.

                    // 2. Generate a verification token or code
                    var verificationToken = GenerateUniqueVerificationToken();  // This is pseudo-code. Implement this function.

                    // 3. Send a confirmation email with a verification link
                   // SendConfirmationEmail(newUser.Email, verificationToken);  // This is pseudo-code. Implement this function.
                }
                catch (Exception ex)
                {
                    // Handle potential errors (e.g., user creation failure, email sending failure)
                    // Depending on your desired behavior, you might rollback the payment or log the error for admin intervention
                    return BadRequest("An error occurred: " + ex.Message);
                }
            }

            return Ok(confirmedPaymentIntent.Status);
        }

        //private ApplicationUser CreateUnverifiedUser(string email)
        //{
        //    var newUser = new ApplicationUser { };
        //    //{
        //    //    Email = email,
        //    //    UserName = email,  // Assuming you use email as username in your system
        //    //    VerificationToken = GenerateUniqueVerificationToken(),
        //    //    VerificationTokenExpiration = DateTime.UtcNow.AddHours(24),  // Token valid for 24 hours
        //    //    IsVerified = false
        //    //};

        //    //_dbContext.Users.Add(newUser);  // Assuming `_context` is an instance of `UserDbContext` available in your class
        //    //_dbContext.SaveChanges();

        //    return newUser;
        //}

        private string GenerateUniqueVerificationToken()
        {
            // This method will generate a unique token for email verification. 
            // This is a simple example using GUIDs, but you might want something more sophisticated depending on your requirements.

            return Guid.NewGuid().ToString();
        }

        private void SendConfirmationEmail(string email, string token)
        {
            // Use your email service to send a confirmation email to the user
            // The email should contain a link that the user can click to verify their account and set up their password
        }



        [HttpPost]
        [Route("stripe-webhook")]
        public IActionResult StripeWebhook()
        {
            var json = new StreamReader(HttpContext.Request.Body).ReadToEnd();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json,
                    Request.Headers["Stripe-Signature"],
                    "your_webhook_signing_secret"
                );

                // Handle different event types accordingly
                if (stripeEvent.Type == Events.PaymentIntentSucceeded)
                {
                    var paymentIntent = (PaymentIntent)stripeEvent.Data.Object;
                    Console.WriteLine($"Payment succeeded: {paymentIntent.Id}");
                }
                // Additional event type handling

                return Ok();
            }
            catch (StripeException e)
            {
                return BadRequest();
            }
        }

    }
    public class PaymentRequest
    {
        public long Amount { get; set; }
    }
    public class ConfirmPaymentRequest
    {
        public string PaymentIntentId { get; set; }
        public string PaymentMethodId { get; set; }

        public string Email { get; set; }

    }


}
