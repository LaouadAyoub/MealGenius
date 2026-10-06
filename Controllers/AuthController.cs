using System.ComponentModel.DataAnnotations;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace MealGeniusBackend.Controllers;

[ApiController, Route("api/[controller]"), EnableRateLimiting("account")]
public class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IAuthService auth, IEmailService email, UserDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("Login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !user.EmailConfirmed) return Unauthorized("Invalid credentials or unconfirmed account.");
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized("Invalid credentials or unconfirmed account.");
        SetCookie(await auth.GenerateToken(user));
        return Ok(new { user.Email, user.PaymentConfirmed });
    }

    [HttpPost("Logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AuthToken", CookieOptions());
        return Ok();
    }

    [HttpPost("ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail(ConfirmationRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null) return BadRequest("Invalid confirmation.");
        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded) return BadRequest("Invalid or expired confirmation.");
        user.EmailConfirmedAt ??= DateTime.UtcNow;
        await users.UpdateAsync(user);
        return await SetupResponse(user);
    }

    [HttpPost("ConfirmAccess")]
    public async Task<IActionResult> ConfirmAccess(AccessRequest request)
    {
        var hash = TokenHash.Compute(request.Token);
        var entry = await db.ConfirmationTokens.AsNoTracking().FirstOrDefaultAsync(t =>
            t.Token == hash && t.TokenType == TokenType.ConfirmAccess && t.IssuedAt > DateTime.UtcNow.AddHours(-3));
        if (entry is null) return BadRequest("Invalid or expired access token.");
        // The conditional delete prevents concurrent reuse of a payment access link.
        if (await db.ConfirmationTokens.Where(t => t.Id == entry.Id).ExecuteDeleteAsync() != 1)
            return BadRequest("Access token already used.");
        var user = await users.FindByIdAsync(entry.UserId);
        if (user is null || !user.PaymentConfirmed) return BadRequest("Invalid access token.");
        user.EmailConfirmed = true;
        user.EmailConfirmedAt ??= DateTime.UtcNow;
        await users.UpdateAsync(user);
        if (!await users.HasPasswordAsync(user)) return await SetupResponse(user);
        SetCookie(await auth.GenerateToken(user));
        return Ok(new { user.Email, user.PaymentConfirmed });
    }

    [HttpPost("SetupPassword"), HttpPost("SetupAccount")]
    public async Task<IActionResult> SetupPassword(PasswordRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || !user.EmailConfirmed || await users.HasPasswordAsync(user))
            return BadRequest("Account cannot be set up; use password reset instead.");
        return await ChangePassword(user, request);
    }

    [HttpPost("ForgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody, Required, EmailAddress] string address)
    {
        var user = await users.FindByEmailAsync(address);
        if (user is not null && user.EmailConfirmed) await email.SendPasswordResetEmail(user);
        return Ok(new { Message = "If the account is eligible, a reset email has been sent." });
    }

    [HttpPost("ResetPassword")]
    public async Task<IActionResult> ResetPassword(PasswordRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || !user.EmailConfirmed) return BadRequest("Invalid reset request.");
        return await ChangePassword(user, request);
    }

    private async Task<IActionResult> ChangePassword(ApplicationUser user, PasswordRequest request)
    {
        // Identity checks both the token and replacement password before changing the hash/security stamp.
        var result = await users.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded) return BadRequest(new { Errors = result.Errors.Select(e => e.Code) });
        SetCookie(await auth.GenerateToken(user));
        return Ok(new { user.Email, user.PaymentConfirmed });
    }

    private async Task<IActionResult> SetupResponse(ApplicationUser user) => Ok(new
    {
        UserId = user.Id, user.Email, user.PaymentConfirmed,
        SetupToken = await users.HasPasswordAsync(user) ? null : await users.GeneratePasswordResetTokenAsync(user),
        Message = "Email confirmed. Use the setup token to set a password, or sign in if already configured."
    });

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax,
        Path = "/", Expires = DateTimeOffset.UtcNow.AddDays(1)
    };
    private void SetCookie(string token) => Response.Cookies.Append("AuthToken", token, CookieOptions());

    public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
    public record ConfirmationRequest([Required] string UserId, [Required] string Token);
    public record AccessRequest([Required] string Token);
    public record PasswordRequest([Required] string UserId, [Required] string Token, [Required] string Password);
}
