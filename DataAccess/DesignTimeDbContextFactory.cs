using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace MealGeniusBackend.DataAcess;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables().Build();
        var connection = configuration["MEALGENIUS_CONNECTIONSTRING"]
            ?? throw new InvalidOperationException("Set MEALGENIUS_CONNECTIONSTRING for migrations.");
        return new UserDbContext(new DbContextOptionsBuilder<UserDbContext>().UseNpgsql(connection).Options);
    }
}
