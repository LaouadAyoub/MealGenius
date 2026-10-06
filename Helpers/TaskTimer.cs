using System.Diagnostics;
using NLog;

public sealed class ServiceTaskTimer(string serviceName, string taskName)
{
    private readonly Stopwatch stopwatch = new();
    public void Start() => stopwatch.Start();
    public void StopAndLog()
    {
        stopwatch.Stop();
        LogManager.GetCurrentClassLogger().Info("Stage {0}/{1} completed in {2} ms",
            serviceName, taskName, stopwatch.ElapsedMilliseconds);
    }
}
