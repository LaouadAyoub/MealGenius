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
using MealGeniusBackend.DataAcess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
using MealGeniusBackend.Services.Dashboard;
using MealGeniusBackend.Services.Auth;
using MealGeniusBackend.Services.RabbitMQ;
using MealGeniusBackend.Models;
using MealGeniusBackend.Middleware;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        try
        {
            // API Controllers and JSON Options
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

            // CORS Policy Configuration
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin", builder =>
                {
                    var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000";

                    builder.WithOrigins(frontendUrl)
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();

                });
            });

            // OpenAI Configuration
            var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            APIAuthentication.Default = new APIAuthentication(openaiApiKey);

            // Database Context Configuration
            var connectionString = Environment.GetEnvironmentVariable("MEALGENIUS_CONNECTIONSTRING");
            //var connectionString = "Host=mealgeniusdb-server.postgres.database.azure.com;Database=mainadmin1234;Username=mainadmin1234;Password=REDACTED";
            services.AddDbContext<UserDbContext>(options => options.UseNpgsql(connectionString));



            // Logging Configuration
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddNLog();
            });

            // FluentEmail configuration
            services
                .AddFluentEmail("redacted@example.invalid")
                .AddMailGunSender(
                Configuration["Mailgun:Domain"],
                Configuration["Mailgun:ApiKey"]
                );

            // Application Services Registration
            RegisterApplicationServices(services);

            // Authentication and Identity Configuration
            ConfigureAuthentication(services);

            // Swagger Generation Configuration
            ConfigureSwagger(services);

            // Azure Blob Storage Configuration
            ConfigureAzureBlobStorage(services);
        }
        catch (Exception ex)
        {
            // Log the exception
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Error(ex, "An error occurred while setting up the services.");
            throw;
        }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        try
        {
            // Environment Configuration
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHttpsRedirection(); // Redirect HTTP to HTTPS.
            }


            // Middleware Configuration
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("AllowSpecificOrigin");
            app.UseAuthentication();
            app.UseAuthorization();

            // Swagger Middleware Configuration
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MealGenius Swagger"));

            // Endpoint Configuration
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
        catch (Exception ex)
        {
            // Log the exception
            var logger = NLog.LogManager.GetCurrentClassLogger();
            logger.Error(ex, "An error occurred while setting up the endpoint routing.");
            throw;
        }
    }

    private void RegisterApplicationServices(IServiceCollection services)
    {
        // Scoped services for application logic
        services.AddScoped<IOpenAIService, OpenAIService>();
        services.AddScoped<IMealPlanService, MealPlanService>();
        services.AddScoped<IUserDashboardService, UserDashboardService>();
        services.AddScoped<IGroceryListService, GroceryListService>();
        services.AddScoped<IMealsImagesService, MealsImagesService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IExecuteTaskService, ExecuteTaskService>();


        // Hosted services
        services.AddHostedService<RabbitMQConsumerHostedService>();

        // Singleton services
        services.AddSingleton<RabbitMQService>();
        services.AddSingleton<OpenAIAPI>();
        services.AddHttpClient<ImageService>();
    }

    private void ConfigureAuthentication(IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {   
            options.SignIn.RequireConfirmedAccount = true; // Changed to false to accept non-confirmed accounts
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false; // No need for non-alphanumeric characters
            options.Password.RequireUppercase = false; // No need for uppercase letters
            options.Password.RequireLowercase = false; // No need for lowercase letters
        })  
        .AddEntityFrameworkStores<UserDbContext>()
        .AddDefaultTokenProviders();


        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            //options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;

        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Configuration["JwtConfig:Issuer"],
                ValidateAudience = true,
                ValidAudience = Configuration["JwtConfig:Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtConfig:Key"]))
            };

            // Retrieve token from cookies
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Cookies["AuthToken"];
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        })
        .AddGoogle(googleOptions =>
        {
            // Your Google authentication configuration
            googleOptions.ClientId = "974872884196-ivamtgv2el9ei0jqtqb5ipul9gn66mqt.apps.googleusercontent.com";
            googleOptions.ClientSecret = "REDACTED";
            googleOptions.CallbackPath = new PathString("/api/Auth/signin-google");
        });
    }

    private void ConfigureSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "MealGenius", Version = "v1" });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });
    }

    private void ConfigureAzureBlobStorage(IServiceCollection services)
    {
        services.AddHttpClient();
        services.Configure<AzureStorageConfig>(Configuration.GetSection("AzureStorageConfig"));
        services.AddSingleton<IAzureBlobService, AzureBlobService>();
    }
}
