using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace MealGeniusBackend.Controllers;

[Route("api/stripe"), ApiController]
public class StripeWebhookController(IConfiguration configuration, UserDbContext db, IExecuteTaskService tasks,
    IEmailService email, ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost("webhook"), RequestSizeLimit(262144)]
    public async Task<IActionResult> Handle()
    {
        var secret = configuration["EndpointSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503, "Stripe is not configured.");
        Event stripeEvent;
        try
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], secret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException) { return BadRequest("Invalid Stripe signature or event."); }
        if (stripeEvent.Type != Events.CheckoutSessionCompleted) return Ok();
        if (stripeEvent.Data.Object is not Stripe.Checkout.Session session || session.PaymentStatus != "paid")
            return Ok();
        var expectedAmount = configuration.GetValue<long>("Stripe:ExpectedAmountTotal");
        var expectedCurrency = configuration["Stripe:Currency"];
        if (expectedAmount <= 0 || string.IsNullOrWhiteSpace(expectedCurrency)) return StatusCode(503, "Stripe pricing is not configured.");
        if (session.AmountTotal != expectedAmount || !string.Equals(session.Currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Unexpected checkout amount or currency.");

        await using var workLock = await DatabaseWorkLock.Acquire(db.Database.GetConnectionString()!,
            "stripe:" + stripeEvent.Id, HttpContext.RequestAborted);
        if (await db.ProcessedStripeEvents.AnyAsync(e => e.Id == stripeEvent.Id)) return Ok();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == session.ClientReferenceId);
        if (user is null || !string.Equals(user.Email, session.CustomerDetails?.Email, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Checkout must reference a registered user with the same email.");
        user.PaymentConfirmed = true;
        user.PaymentConfirmedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        // Payment is committed before queueing. Replay can republish after a crash; the job processor deduplicates by task ID.
        await tasks.ExecuteUserTask(user.Email!);
        await email.SendPaymentConfirmationEmail(user.Email!);
        db.ProcessedStripeEvents.Add(new ProcessedStripeEvent { Id = stripeEvent.Id, ProcessedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        logger.LogInformation("Stripe event {EventId} processed.", stripeEvent.Id);
        return Ok();
    }
}
