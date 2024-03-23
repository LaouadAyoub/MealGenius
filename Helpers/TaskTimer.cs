using System.Diagnostics;
using System.IO;

public class ServiceTaskTimer
{
    private readonly Stopwatch _stopwatch;
    private readonly string _serviceName;
    private readonly string _taskName;

    public ServiceTaskTimer(string serviceName, string taskName)
    {
        _serviceName = serviceName;
        _taskName = taskName;
        _stopwatch = new Stopwatch();
    }

    public void Start()
    {
        _stopwatch.Start();
    }

    public void StopAndLog()
    {
        _stopwatch.Stop();
        var elapsedTime = _stopwatch.ElapsedMilliseconds;

        // Log the result to a text file, specifying the service and task
        LogTimeToFile($"Service: {_serviceName}, Task: {_taskName} took {elapsedTime} ms");
    }

    private void LogTimeToFile(string message)
    {
        // Specify your path and filename
        string path = "ServiceTaskTimings.txt";

        // Create a file to write to, if the file exists it appends the text to the file
        using (StreamWriter sw = File.AppendText(path))
        {
            sw.WriteLine($"{System.DateTime.Now}: {message}");
        }
    }
}
