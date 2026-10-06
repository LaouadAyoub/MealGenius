using NLog;

public class Program
{
    public static void Main(string[] args)
    {
        try { CreateHostBuilder(args).Build().Run(); }
        catch (Exception ex)
        {
            LogManager.GetCurrentClassLogger().Error(ex, "Application stopped unexpectedly.");
            throw;
        }
        finally { LogManager.Shutdown(); }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
}
