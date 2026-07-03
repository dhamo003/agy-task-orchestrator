using System;
using System.IO;

namespace Runner.Logging;

public class FileTaskLogger : ITaskLogger
{
    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly object _lock = new();

    public FileTaskLogger(string logDirectory, LogLevel minimumLevel = LogLevel.Trace)
    {
        _logDirectory = logDirectory;
        _minimumLevel = minimumLevel;
        
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public void Log(LogLevel level, TaskLogScope scope, string message, Exception? exception = null)
    {
        if (level < _minimumLevel)
            return;

        var entry = new TaskLogEntry(DateTimeOffset.Now, level, scope, message, exception);
        var json = JsonLogFormatter.Format(entry);
        
        var safeTaskId = string.Join("_", scope.TaskId.Split(Path.GetInvalidFileNameChars()));
        var filePath = Path.Combine(_logDirectory, $"task_{safeTaskId}.log");

        lock (_lock)
        {
            File.AppendAllText(filePath, json + Environment.NewLine);
        }
    }
}
