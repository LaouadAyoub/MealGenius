using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using OpenAI_API;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MealGeniusBackend.Models.UserModel;
using MealGeniusBackend.DataAccess;


internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder =>
                {
                    builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
                });
        });
        // Set the default API authentication using the environment variable
        APIAuthentication.Default = new APIAuthentication(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        builder.Services.AddSingleton<OpenAIAPI>();
        builder.Services.AddScoped<IOpenAIService, OpenAIService>();
        builder.Services.AddDbContext<UserDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection")));


        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<UserDbContext>()
                .AddDefaultTokenProviders();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    // Validate the token issuer
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["JWT:Issuer"],

                    // Validate the token audience
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["JWT:Audience"],

                    // Validate the token expiry
                    ValidateLifetime = true,

                    // Validate the token signing key
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"]))
                };
            });
        builder.Services.AddTransient<UserDbContextSeeder>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<UserDbContextSeeder>();
            seeder.SeedDatabase().Wait();
        }
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.UseCors("AllowAll");


        app.Run();
    }
}