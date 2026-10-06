using System.Net;
using System.Net.Http.Json;
using MealGeniusBackend.Controllers;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace MealGeniusBackend.Tests;

public sealed class SecurityTests
{
    [Theory]
    [InlineData("/api/MainAPI/delete-user-by-email")]
    [InlineData("/api/Auth/ConfirmEmailbyEmail")]
    [InlineData("/api/Auth/SetupPasswordByEmail")]
    [InlineData("/create-payment-intent")]
    public async Task Obsolete_dangerous_routes_are_not_available(string path)
    {
        await using var factory = new TestApplication();
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync(path, new { Email = "someone@example.test" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_mutations_require_the_browser_header()
    {
        await using var factory = new TestApplication();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/Logout", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Setup_requires_an_ownership_token_and_does_not_replace_an_existing_password()
    {
        await using var factory = new TestApplication();
        var user = await factory.AddUser(password: null);
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/Auth/SetupPassword",
            new AuthController.PasswordRequest(user.Id, "invalid-token", TestApplication.Password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.False(await users.HasPasswordAsync((await users.FindByIdAsync(user.Id))!));
        var token = await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(user.Id))!);
        response = await client.PostAsJsonAsync("/api/Auth/SetupPassword",
            new AuthController.PasswordRequest(user.Id, token, TestApplication.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/Auth/SetupPassword",
            new AuthController.PasswordRequest(user.Id, token, "Different-Password9!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_replacement_password_preserves_the_old_hash_and_valid_reset_revokes_old_session()
    {
        await using var factory = new TestApplication();
        var user = await factory.AddUser(TestApplication.Password);
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/Login",
            new AuthController.LoginRequest(user.Email!, TestApplication.Password))).StatusCode);
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            token = await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(user.Id))!);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Auth/ResetPassword",
            new AuthController.PasswordRequest(user.Id, token, "short"))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(user.Id))!, TestApplication.Password));
        }
        using var resetClient = factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await resetClient.PostAsJsonAsync("/api/Auth/ResetPassword",
            new AuthController.PasswordRequest(user.Id, token, "Replacement-Password9!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Status/GetStatus")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await resetClient.PostAsJsonAsync("/api/Auth/ResetPassword",
            new AuthController.PasswordRequest(user.Id, token, "Another-Password9!"))).StatusCode);
    }

    [Fact]
    public async Task Token_for_another_account_cannot_reset_the_target()
    {
        await using var factory = new TestApplication();
        var owner = await factory.AddUser(TestApplication.Password);
        var target = await factory.AddUser(TestApplication.Password);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var token = await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(owner.Id))!);
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/Auth/ResetPassword",
            new AuthController.PasswordRequest(target.Id, token, "Replacement-Password9!"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unpaid_accounts_cannot_enqueue_generation()
    {
        await using var factory = new TestApplication();
        var user = await factory.AddUser(TestApplication.Password, paid: false);
        using var client = factory.Client();
        await client.PostAsJsonAsync("/api/Auth/Login", new AuthController.LoginRequest(user.Email!, TestApplication.Password));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/MainAPI/ExecuteTask", new { })).StatusCode);
    }

    [Fact]
    public async Task Invalid_questionnaire_returns_400_before_external_services_are_called()
    {
        await using var factory = new TestApplication();
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/MainAPI/RegisterUser", new { Email = "not-an-email", Age = "unknown" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public sealed class TestApplication : WebApplicationFactory<Program>
{
    public const string Password = "Synthetic-Test-Password9!";
    private readonly string database = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MEALGENIUS_CONNECTIONSTRING"] = "Host=localhost;Database=unused",
            ["JwtConfig:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
            ["JwtConfig:Issuer"] = "test", ["JwtConfig:Audience"] = "test",
            ["Messaging:Enabled"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<UserDbContext>>();
            services.AddDbContext<UserDbContext>(o => o.UseInMemoryDatabase(database));
            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, NoEmail>();
        });
    }
    public HttpClient Client()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-MealGenius-Client", "web");
        return client;
    }
    public async Task<ApplicationUser> AddUser(string? password, bool paid = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = Guid.NewGuid() + "@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, PaymentConfirmed = paid };
        var result = password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Code)));
        return user;
    }
    private sealed class NoEmail : IEmailService
    {
        public Task SendConfirmationEmail(ApplicationUser user, string name) => Task.CompletedTask;
        public Task SendPaymentConfirmationEmail(string email) => Task.CompletedTask;
        public Task SendPasswordResetEmail(ApplicationUser user) => Task.CompletedTask;
    }
}
