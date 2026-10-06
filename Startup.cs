using System.Security.Claims;
using System.Text;
using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services;
using MealGeniusBackend.Services.Auth;
using MealGeniusBackend.Services.Dashboard;
using MealGeniusBackend.Services.RabbitMQ;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NLog.Extensions.Logging;
using System.Threading.RateLimiting;

public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        var database = Required("MEALGENIUS_CONNECTIONSTRING");
        var jwtKey = Required("JwtConfig:Key");
        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            throw new InvalidOperationException("JwtConfig:Key must contain at least 32 UTF-8 bytes.");
        services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);
        services.AddProblemDetails();
        services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(configuration["FRONTEND_URL"] ?? "http://localhost:3000")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        services.AddDbContext<UserDbContext>(o => o.UseNpgsql(database));
        services.AddLogging(b => { b.ClearProviders(); b.AddNLog(); });
        services.AddFluentEmail(configuration["Mailgun:From"] ?? "noreply@example.com")
            .AddMailGunSender(configuration["Mailgun:Domain"] ?? "", configuration["Mailgun:ApiKey"] ?? "");
        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.SignIn.RequireConfirmedAccount = true;
            o.Password.RequiredLength = 12;
            o.Lockout.MaxFailedAccessAttempts = 5;
        }).AddEntityFrameworkStores<UserDbContext>().AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = Required("JwtConfig:Issuer"),
                ValidateAudience = true, ValidAudience = Required("JwtConfig:Audience"),
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = c =>
                {
                    if (c.Request.Cookies.TryGetValue("AuthToken", out var token)) c.Token = token;
                    return Task.CompletedTask;
                },
                OnTokenValidated = async c =>
                {
                    var users = c.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await users.FindByIdAsync(c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
                    if (user is null || !user.EmailConfirmed || user.SecurityStamp != c.Principal.FindFirstValue("security_stamp"))
                        c.Fail("Account or session is no longer valid.");
                    else if (!user.PaymentConfirmed && c.Principal.HasClaim("paid", "true"))
                        c.Fail("Payment entitlement changed; sign in again.");
                }
            };
        });
        services.AddAuthorization(o => o.AddPolicy("PaidUser", p => p.RequireAuthenticatedUser().RequireClaim("paid", "true")));
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        services.AddScoped<IMealPlanService, MealPlanService>();
        services.AddScoped<IUserDashboardService, UserDashboardService>();
        services.AddScoped<IGroceryListService, GroceryListService>();
        services.AddScoped<IMealsImagesService, MealsImagesService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IExecuteTaskService, ExecuteTaskService>();
        services.AddScoped<GenerationJobProcessor>();
        services.AddSingleton<GenerationCancellation>();
        services.AddSingleton<OpenAIConcurrency>();
        services.AddHttpClient<IOpenAIService, OpenAIService>(c => c.Timeout = TimeSpan.FromMinutes(3));
        services.Configure<AzureStorageConfig>(configuration.GetSection("AzureStorageConfig"));
        services.AddHttpClient<IAzureBlobService, AzureBlobService>();
        services.AddSingleton<RabbitMQService>();
        if (configuration.GetValue("Messaging:Enabled", true))
            services.AddHostedService<RabbitMQConsumerHostedService>();
        services.AddSwaggerGen();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseExceptionHandler();
        if (!environment.IsDevelopment()) app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        // Browser forms cannot send this custom header. Stripe authenticates with its signature.
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                && !HttpMethods.IsOptions(context.Request.Method)
                && context.Request.Path != "/api/stripe/webhook"
                && context.Request.Headers["X-MealGenius-Client"] != "web")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next();
        });
        app.UseAuthentication();
        app.UseAuthorization();
        if (environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        app.UseEndpoints(e => e.MapControllers());
    }

    private string Required(string key) => !string.IsNullOrWhiteSpace(configuration[key])
        ? configuration[key]! : throw new InvalidOperationException($"Missing configuration: {key}");
}
