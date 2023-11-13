using MealGeniusBackend.DataAcess;
using MealGeniusBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using OpenAI_API;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
//using MealGeniusBackend.Models.UserModel;
using MealGeniusBackend.DataAccess;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using NLog;
using ILogger = NLog.ILogger;

internal class Program
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    private static void Main(string[] args)
    {
        try
        {
            CreateHostBuilder(args).Build().Run();
            Logger.Info("Application ran successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while running the application.");
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                var port = Environment.GetEnvironmentVariable("PORT");
                if (!string.IsNullOrEmpty(port)) // Running on Heroku
                {
                    Logger.Info("Running on Heroku : ", port);
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://*:" + port);
                }
                else // Running locally
                {
                    Logger.Info("Running locally.");
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://*:5139");
                }
            });
}