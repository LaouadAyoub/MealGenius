using MealGeniusBackend.DataAccess;
using MealGeniusBackend.DataAcess;
//using MealGeniusBackend.Models.UserModel;
using MealGeniusBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NLog.Extensions.Logging;
using OpenAI_API;
using Stripe;
using System.Text;
using FluentEmail.Mailgun;
using Microsoft.OpenApi.Models;
using System.Reflection;


public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        //var stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
        
        //StripeConfiguration.ApiKey = stripeSecretKey;

        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        });

        services.AddCors(options =>
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

        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var connectionString = Environment.GetEnvironmentVariable("MEALGENIUS_CONNECTIONSTRING");
        services.AddDbContext<UserDbContext>(options =>
            options.UseNpgsql(
                Environment.GetEnvironmentVariable("MEALGENIUS_CONNECTIONSTRING")));


        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddNLog();
        });


        services.AddScoped<IOpenAIService, OpenAIService>();

        services.AddScoped<IMealPlanService, MealPlanService>();
        services.AddScoped<IUserDashboardService, UserDashboardService>();
        services.AddScoped<IGroceryListService, GroceryListService>();
        services.AddScoped<IMealsImagesService , MealsImagesService> ();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddHostedService<RabbitMQConsumerHostedService>();
        services.AddSingleton<RabbitMQService>();
        services.AddSingleton<OpenAIAPI>();
        services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<UserDbContext>()
                .AddDefaultTokenProviders();

        // Set up FluentEmail services
        services
            .AddFluentEmail("redacted@example.invalid")
            .AddMailGunSender(
            Configuration["Mailgun:Domain"],
            Configuration["Mailgun:ApiKey"]
        );
        services.AddScoped<IEmailService, EmailService>();
        services.AddHttpClient<ImageService>();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "MealGenius", Version = "v1" });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });


        services.AddAuthentication(
            options => 
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;            }
            )
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    // Validate the token issuer
                    ValidateIssuer = true,
                    ValidIssuer = Configuration["JwtConfig:Issuer"],

                    // Validate the token audience
                    ValidateAudience = true,
                    ValidAudience = Configuration["JwtConfig:Audience"],

                    // Validate the token expiry
                    ValidateLifetime = true,

                    // Validate the token signing key
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtConfig:Key"]))
                };
            });
        services.AddTransient<UserDbContextSeeder>();

        // configure azure blob storage
        services.AddHttpClient();
        services.Configure<AzureStorageConfig>(Configuration.GetSection("AzureStorageConfig"));
        services.AddSingleton<IAzureBlobService, AzureBlobService>();


    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors("AllowAll");
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "MealGenius Swagger");
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
