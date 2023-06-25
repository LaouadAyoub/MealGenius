using MealGeniusBackend.Services;
using OpenAI_API;

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
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthorization();

        app.MapControllers();
        app.UseCors("AllowAll");


        app.Run();
    }
}