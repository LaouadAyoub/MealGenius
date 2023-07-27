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
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;


internal class Program
{
    private static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                var port = Environment.GetEnvironmentVariable("PORT");
                if (!string.IsNullOrEmpty(port)) // Running on Heroku
                {
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://*:" + port);
                }
                else // Running locally
                {
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://localhost:5139");
                }
            });
}