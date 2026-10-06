using System.Security.Claims;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Mapper;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MealGeniusBackend.Controllers;

[Route("api/[controller]"), ApiController]
public class MainAPIController(UserDbContext db, UserManager<ApplicationUser> users,
    IUserService userService, IEmailService email, IExecuteTaskService tasks) : ControllerBase
{
    [HttpPost("RegisterUser"), EnableRateLimiting("account")]
    public async Task<IActionResult> RegisterUser(UserProfile profile)
    {
        var user = await users.FindByEmailAsync(profile.Email);
        if (user is not null)
            return Ok(new { Message = "If you already registered, use your confirmation email or password reset." });
        var result = await userService.CreateUserAsync(profile.Email, profile.Name);
        if (!result.Result.Succeeded) return BadRequest(new { Errors = result.Result.Errors.Select(e => e.Code) });
        var input = new UserInput
        {
            UserId = result.User.Id, UserData = JsonConvert.SerializeObject(DtoMapper.MapUserProfileToUserData(profile)),
            Task = new UserTask { Id = Guid.NewGuid(), UserId = result.User.Id }
        };
        db.UserInputs.Add(input);
        await db.SaveChangesAsync();
        await email.SendConfirmationEmail(result.User, profile.Name);
        return Ok(new { Message = "Check your email to confirm your account." });
    }

    [HttpPost("ResendConfirmation"), EnableRateLimiting("account")]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && !user.EmailConfirmed
            && (user.ConfirmationEmailSentAt is null || user.ConfirmationEmailSentAt < DateTime.UtcNow.AddMinutes(-3)))
            await email.SendConfirmationEmail(user, user.FirstName ?? "");
        return Ok(new { Message = "If eligible, a confirmation email has been sent." });
    }

    public record EmailRequest([System.ComponentModel.DataAnnotations.Required,
        System.ComponentModel.DataAnnotations.EmailAddress] string Email);

    [Authorize(Policy = "PaidUser"), HttpPost("ExecuteTask")]
    public async Task<IActionResult> ExecuteTask()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return user is not null && await tasks.ExecuteUserTask(user.Email!)
            ? Accepted(new { Message = "Generation queued. Poll /api/Status/GetStatus." })
            : Conflict("No eligible generation task.");
    }
}
